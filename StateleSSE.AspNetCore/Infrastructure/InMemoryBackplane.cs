using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StateleSSE.AspNetCore;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// In-memory implementation of ISseBackplane for single-server deployments.
/// Ideal for development, testing, and applications that don't require horizontal scaling.
/// </summary>
public class InMemoryBackplane(ILogger<InMemoryBackplane> logger) : ISseBackplane, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _groupSubscribers = new();

    /// <summary>
    /// Creates an InMemoryBackplane instance without logging.
    /// </summary>
    public InMemoryBackplane() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBackplane>.Instance)
    {
    }

    /// <inheritdoc/>
    public (ChannelReader<object> Reader, Guid ConnectionId) OpenConnection()
    {
        var channel = Channel.CreateUnbounded<object>();
        var connectionId = Guid.NewGuid();
        var state = new ConnectionState(channel);

        _connections.TryAdd(connectionId, state);

        logger.LogDebug("Opened connection {ConnectionId}. Total connections: {Count}",
            connectionId, _connections.Count);

        return (channel.Reader, connectionId);
    }

    /// <inheritdoc/>
    public bool AddSubscription(Guid connectionId, string groupId)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            logger.LogWarning("AddSubscription failed: connection {ConnectionId} not found", connectionId);
            return false;
        }

        // Check if already subscribed
        if (!state.SubscribedGroups.TryAdd(groupId, 0))
        {
            logger.LogDebug("Connection {ConnectionId} already subscribed to group '{GroupId}'",
                connectionId, groupId);
            return false;
        }

        // Add to group's subscriber list
        var subscribers = _groupSubscribers.GetOrAdd(groupId, _ => new ConcurrentDictionary<Guid, byte>());
        subscribers.TryAdd(connectionId, 0);

        logger.LogDebug("Connection {ConnectionId} subscribed to group '{GroupId}'. Group now has {Count} subscribers",
            connectionId, groupId, subscribers.Count);

        return true;
    }

    /// <inheritdoc/>
    public bool RemoveSubscription(Guid connectionId, string groupId)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            logger.LogWarning("RemoveSubscription failed: connection {ConnectionId} not found", connectionId);
            return false;
        }

        if (!state.SubscribedGroups.TryRemove(groupId, out _))
        {
            logger.LogDebug("Connection {ConnectionId} was not subscribed to group '{GroupId}'",
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
                logger.LogDebug("No subscribers left for group '{GroupId}', cleaning up", groupId);
            }
        }

        logger.LogDebug("Connection {ConnectionId} unsubscribed from group '{GroupId}'",
            connectionId, groupId);

        return true;
    }

    /// <inheritdoc/>
    public void CloseConnection(Guid connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var state))
        {
            logger.LogDebug("CloseConnection: connection {ConnectionId} not found (may already be closed)",
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

        logger.LogDebug("Closed connection {ConnectionId}. Removed from {GroupCount} groups. Total connections: {Count}",
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
        if (_groupSubscribers.TryGetValue(groupId, out var subscribers))
        {
            logger.LogDebug("Publishing to {Count} subscribers in group '{GroupId}': {MessageType}",
                subscribers.Count, groupId, message.GetType().Name);

            var tasks = new List<Task>();

            foreach (var connectionId in subscribers.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var state))
                {
                    tasks.Add(state.Channel.Writer.WriteAsync(message).AsTask());
                }
            }

            await Task.WhenAll(tasks);
        }
        else
        {
            logger.LogDebug("Published to group '{GroupId}', but no subscribers", groupId);
        }
    }

    /// <inheritdoc/>
    public async Task PublishToGroups(IEnumerable<string> groupIds, object message)
    {
        // Collect unique connection IDs to avoid sending duplicates
        var connectionIds = new HashSet<Guid>();

        foreach (var groupId in groupIds)
        {
            if (_groupSubscribers.TryGetValue(groupId, out var subscribers))
            {
                foreach (var connectionId in subscribers.Keys)
                {
                    connectionIds.Add(connectionId);
                }
            }
        }

        logger.LogDebug("Publishing to {Count} unique connections across multiple groups: {MessageType}",
            connectionIds.Count, message.GetType().Name);

        var tasks = new List<Task>();

        foreach (var connectionId in connectionIds)
        {
            if (_connections.TryGetValue(connectionId, out var state))
            {
                tasks.Add(state.Channel.Writer.WriteAsync(message).AsTask());
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public async Task PublishToAll(object message)
    {
        logger.LogDebug("Broadcasting to ALL connections: {MessageType}", message.GetType().Name);

        var tasks = _connections.Values.Select(state =>
            state.Channel.Writer.WriteAsync(message).AsTask()
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
        foreach (var state in _connections.Values)
        {
            state.Channel.Writer.Complete();
        }

        _connections.Clear();
        _groupSubscribers.Clear();
        logger.LogDebug("Disposed");
    }

    private sealed class ConnectionState(Channel<object> channel)
    {
        public Channel<object> Channel { get; } = channel;
        public ConcurrentDictionary<string, byte> SubscribedGroups { get; } = new();
    }
}
