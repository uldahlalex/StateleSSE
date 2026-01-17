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

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<object>>> _localSubscribers = new();

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

    /// <summary>
    /// Subscribe a local client (SSE connection) to a group.
    /// Returns channel reader and unique subscriber ID.
    /// </summary>
    public (ChannelReader<object> reader, Guid subscriberId) Subscribe(string groupId)
    {
        var channel = Channel.CreateUnbounded<object>();
        var subscriberId = Guid.NewGuid();

        var channels = _localSubscribers.GetOrAdd(groupId, _ => new ConcurrentDictionary<Guid, Channel<object>>());
        channels.TryAdd(subscriberId, channel);

        _logger.LogDebug("New subscriber {SubscriberId} for group '{GroupId}'. Total local: {Count}",
            subscriberId, groupId, channels.Count);

        return (channel.Reader, subscriberId);
    }

    /// <summary>
    /// Unsubscribe a local client (cleanup when SSE connection closes).
    /// </summary>
    public void Unsubscribe(string groupId, Guid subscriberId)
    {
        if (!_localSubscribers.TryGetValue(groupId, out var channels))
            return;

        if (channels.TryRemove(subscriberId, out var channel))
        {
            channel.Writer.Complete();
            _logger.LogDebug("Unsubscribed {SubscriberId} from group '{GroupId}'. Remaining local: {Count}",
                subscriberId, groupId, channels.Count);
        }

        if (channels.IsEmpty)
        {
            _localSubscribers.TryRemove(groupId, out _);
            _logger.LogDebug("No local subscribers left for group '{GroupId}', cleaning up", groupId);
        }
    }

    /// <summary>
    /// Publish message to ALL servers in a group via Redis pub/sub.
    /// </summary>
    public async Task PublishToGroup(string groupId, object message)
    {
        var envelope = new BackplaneEnvelope
        {
            GroupId = groupId,
            Payload = message,
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

    /// <summary>
    /// Publish message to multiple groups at once.
    /// </summary>
    public async Task PublishToGroups(IEnumerable<string> groupIds, object message)
    {
        var tasks = groupIds.Select(groupId => PublishToGroup(groupId, message));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Publish message to ALL groups (broadcast to entire system).
    /// </summary>
    public async Task PublishToAll(object message)
    {
        var envelope = new BackplaneEnvelope
        {
            GroupId = "*",
            Payload = message,
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
                await BroadcastToAllLocalGroups(envelope.Payload);
                return;
            }

            if (_localSubscribers.TryGetValue(envelope.GroupId, out var channels))
            {
                _logger.LogDebug("Forwarding Redis event to {Count} local subscribers of group '{GroupId}'",
                    channels.Count, envelope.GroupId);

                var tasks = channels.Values.Select(channel =>
                    channel.Writer.WriteAsync(envelope.Payload).AsTask()
                );

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

    private async Task BroadcastToAllLocalGroups(object payload)
    {
        var allTasks = new List<Task>();

        foreach (var (groupId, channels) in _localSubscribers)
        {
            _logger.LogDebug("Broadcasting to {Count} local subscribers in group '{GroupId}'",
                channels.Count, groupId);

            var tasks = channels.Values.Select(channel =>
                channel.Writer.WriteAsync(payload).AsTask()
            );

            allTasks.AddRange(tasks);
        }

        await Task.WhenAll(allTasks);
    }

    /// <summary>
    /// Get count of local subscribers for a group (only on THIS server).
    /// </summary>
    public int GetLocalSubscriberCount(string groupId)
    {
        return _localSubscribers.TryGetValue(groupId, out var channels) ? channels.Count : 0;
    }

    /// <summary>
    /// Get all group IDs that have local subscribers on THIS server.
    /// </summary>
    public IEnumerable<string> GetLocalGroups()
    {
        return _localSubscribers.Keys;
    }

    /// <summary>
    /// Get diagnostic info about this server's backplane state.
    /// </summary>
    public BackplaneDiagnostics GetDiagnostics()
    {
        return new BackplaneDiagnostics
        {
            TotalGroups = _localSubscribers.Count,
            TotalLocalSubscribers = _localSubscribers.Values.Sum(c => c.Count),
            Groups = _localSubscribers.Select(kvp => new GroupInfo
            {
                GroupId = kvp.Key,
                LocalSubscribers = kvp.Value.Count
            }).ToArray()
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriber.Unsubscribe((RedisChannel)$"{_channelPrefix}:events");

        foreach (var groupChannels in _localSubscribers.Values)
        {
            foreach (var channel in groupChannels.Values)
            {
                channel.Writer.Complete();
            }
        }

        _localSubscribers.Clear();
        _logger.LogDebug("Disposed (prefix: '{ChannelPrefix}')", _channelPrefix);
    }
}

internal class BackplaneEnvelope
{
    public required string GroupId { get; init; }
    public required object Payload { get; init; }
    public DateTime PublishedAt { get; init; }
}
