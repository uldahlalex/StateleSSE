using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// Redis-based implementation of ISseBackplane for horizontal scaling of SSE/realtime features.
/// </summary>
public class RedisBackplane : ISseBackplane, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISubscriber _subscriber;
    private readonly string _channelPrefix;
    private readonly ILogger<RedisBackplane> _logger;

    private readonly ConcurrentDictionary<Guid, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _groupSubscribers = new();

    /// <summary>
    /// Creates a RedisBackplane instance.
    /// </summary>
    public RedisBackplane(IConnectionMultiplexer redis, ILogger<RedisBackplane> logger, string channelPrefix = "backplane")
    {
        _redis = redis;
        _subscriber = redis.GetSubscriber();
        _channelPrefix = channelPrefix;
        _logger = logger;

        _subscriber.Subscribe(
            (RedisChannel)$"{_channelPrefix}:events",
            async (channel, message) =>
            {
                try
                {
                    await OnRedisMessage(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OnRedisMessage");
                }
            }
        );

        _logger.LogInformation("Initialized with prefix '{ChannelPrefix}', subscribed to channel: {Channel}",
            _channelPrefix, $"{_channelPrefix}:events");
    }

    /// <inheritdoc/>
    public (ChannelReader<object> Reader, Guid ConnectionId) OpenConnection()
    {
        var channel = Channel.CreateUnbounded<object>();
        var connectionId = Guid.NewGuid();
        var state = new ConnectionState(channel);

        _connections.TryAdd(connectionId, state);

        _logger.LogDebug("Opened connection {ConnectionId}. Total local connections: {Count}",
            connectionId, _connections.Count);

        return (channel.Reader, connectionId);
    }

    /// <inheritdoc/>
    public bool AddSubscription(Guid connectionId, string groupId)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            _logger.LogWarning("AddSubscription failed: connection {ConnectionId} not found", connectionId);
            return false;
        }

        // Check if already subscribed
        if (!state.SubscribedGroups.TryAdd(groupId, 0))
        {
            _logger.LogDebug("Connection {ConnectionId} already subscribed to group '{GroupId}'",
                connectionId, groupId);
            return false;
        }

        // Add to group's subscriber list
        var subscribers = _groupSubscribers.GetOrAdd(groupId, _ => new ConcurrentDictionary<Guid, byte>());
        subscribers.TryAdd(connectionId, 0);

        _logger.LogDebug("Connection {ConnectionId} subscribed to group '{GroupId}'. Group now has {Count} local subscribers",
            connectionId, groupId, subscribers.Count);

        return true;
    }

    /// <inheritdoc/>
    public bool RemoveSubscription(Guid connectionId, string groupId)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            _logger.LogWarning("RemoveSubscription failed: connection {ConnectionId} not found", connectionId);
            return false;
        }

        if (!state.SubscribedGroups.TryRemove(groupId, out _))
        {
            _logger.LogDebug("Connection {ConnectionId} was not subscribed to group '{GroupId}'",
                connectionId, groupId);
            return false;
        }

        // Remove from group's subscriber list
        if (_groupSubscribers.TryGetValue(groupId, out var subscribers))
        {
            subscribers.TryRemove(connectionId, out _);

            if (subscribers.IsEmpty)
            {
                _groupSubscribers.TryRemove(groupId, out _);
                _logger.LogDebug("No local subscribers left for group '{GroupId}', cleaning up", groupId);
            }
        }

        _logger.LogDebug("Connection {ConnectionId} unsubscribed from group '{GroupId}'",
            connectionId, groupId);

        return true;
    }

    /// <inheritdoc/>
    public void CloseConnection(Guid connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var state))
        {
            _logger.LogDebug("CloseConnection: connection {ConnectionId} not found (may already be closed)",
                connectionId);
            return;
        }

        // Remove from all subscribed groups
        foreach (var groupId in state.SubscribedGroups.Keys)
        {
            if (_groupSubscribers.TryGetValue(groupId, out var subscribers))
            {
                subscribers.TryRemove(connectionId, out _);

                if (subscribers.IsEmpty)
                {
                    _groupSubscribers.TryRemove(groupId, out _);
                }
            }
        }

        state.Channel.Writer.Complete();

        _logger.LogDebug("Closed connection {ConnectionId}. Removed from {GroupCount} groups. Total local connections: {Count}",
            connectionId, state.SubscribedGroups.Count, _connections.Count);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetSubscriptions(Guid connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            return Array.Empty<string>();
        }

        return state.SubscribedGroups.Keys.ToArray();
    }

    /// <inheritdoc/>
    public async Task PublishToGroup(string groupId, object message)
    {
        var payloadJson = JsonSerializer.Serialize(message);
        var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        var envelope = new BackplaneEnvelope
        {
            GroupId = groupId,
            Payload = payloadElement,
            PublishedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(envelope);

        await _subscriber.PublishAsync(
            (RedisChannel)$"{_channelPrefix}:events",
            json
        );

        _logger.LogDebug("Published to Redis for group '{GroupId}': {MessageType}",
            groupId, message.GetType().Name);
    }

    /// <inheritdoc/>
    public async Task PublishToGroups(IEnumerable<string> groupIds, object message)
    {
        var tasks = groupIds.Select(groupId => PublishToGroup(groupId, message));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public async Task PublishToAll(object message)
    {
        var payloadJson = JsonSerializer.Serialize(message);
        var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        var envelope = new BackplaneEnvelope
        {
            GroupId = "*",
            Payload = payloadElement,
            PublishedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(envelope);

        await _subscriber.PublishAsync(
            (RedisChannel)$"{_channelPrefix}:events",
            json
        );

        _logger.LogDebug("Published to ALL groups: {MessageType}", message.GetType().Name);
    }

    private async Task OnRedisMessage(RedisValue message)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<BackplaneEnvelope>(message.ToString());
            if (envelope == null) return;

            if (envelope.GroupId == "*")
            {
                await BroadcastToAllLocalConnections(envelope.Payload);
                return;
            }

            if (_groupSubscribers.TryGetValue(envelope.GroupId, out var subscribers))
            {
                _logger.LogDebug("Forwarding Redis event to {Count} local connections subscribed to group '{GroupId}'",
                    subscribers.Count, envelope.GroupId);

                var tasks = new List<Task>();

                foreach (var connectionId in subscribers.Keys)
                {
                    if (_connections.TryGetValue(connectionId, out var state))
                    {
                        tasks.Add(state.Channel.Writer.WriteAsync(envelope.Payload).AsTask());
                    }
                }

                await Task.WhenAll(tasks);
            }
            else
            {
                _logger.LogDebug("Received Redis event for group '{GroupId}', but no local subscribers",
                    envelope.GroupId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Redis message");
        }
    }

    private async Task BroadcastToAllLocalConnections(object payload)
    {
        _logger.LogDebug("Broadcasting to {Count} local connections", _connections.Count);

        var tasks = _connections.Values.Select(state =>
            state.Channel.Writer.WriteAsync(payload).AsTask()
        );

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public int GetLocalSubscriberCount(string groupId)
    {
        return _groupSubscribers.TryGetValue(groupId, out var subscribers) ? subscribers.Count : 0;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetLocalGroups()
    {
        return _groupSubscribers.Keys;
    }

    /// <inheritdoc/>
    public BackplaneDiagnostics GetDiagnostics()
    {
        return new BackplaneDiagnostics
        {
            TotalConnections = _connections.Count,
            TotalGroups = _groupSubscribers.Count,
            TotalSubscriptions = _connections.Values.Sum(c => c.SubscribedGroups.Count),
            Groups = _groupSubscribers.Select(kvp => new GroupInfo
            {
                GroupId = kvp.Key,
                SubscriberCount = kvp.Value.Count
            }).ToArray(),
            Connections = _connections.Select(kvp => new ConnectionInfo
            {
                ConnectionId = kvp.Key,
                SubscriptionCount = kvp.Value.SubscribedGroups.Count,
                SubscribedGroups = kvp.Value.SubscribedGroups.Keys.ToArray()
            }).ToArray()
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriber.Unsubscribe((RedisChannel)$"{_channelPrefix}:events");

        foreach (var state in _connections.Values)
        {
            state.Channel.Writer.Complete();
        }

        _connections.Clear();
        _groupSubscribers.Clear();
        _logger.LogDebug("Disposed (prefix: '{ChannelPrefix}')", _channelPrefix);
    }

    private sealed class ConnectionState(Channel<object> channel)
    {
        public Channel<object> Channel { get; } = channel;
        public ConcurrentDictionary<string, byte> SubscribedGroups { get; } = new();
    }
}

internal class BackplaneEnvelope
{
    public required string GroupId { get; init; }
    public required JsonElement Payload { get; init; }
    public DateTime PublishedAt { get; init; }
}
