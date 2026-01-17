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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<object>>> _localSubscribers = new();

    /// <summary>
    /// Creates an InMemoryBackplane instance without logging.
    /// </summary>
    public InMemoryBackplane() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBackplane>.Instance)
    {
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

        logger.LogDebug("New subscriber {SubscriberId} for group '{GroupId}'. Total local: {Count}",
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
            logger.LogDebug("Unsubscribed {SubscriberId} from group '{GroupId}'. Remaining local: {Count}",
                subscriberId, groupId, channels.Count);
        }

        if (channels.IsEmpty)
        {
            _localSubscribers.TryRemove(groupId, out _);
            logger.LogDebug("No subscribers left for group '{GroupId}', cleaning up", groupId);
        }
    }

    /// <summary>
    /// Publish message to all subscribers in a group.
    /// </summary>
    public async Task PublishToGroup(string groupId, object message)
    {
        if (_localSubscribers.TryGetValue(groupId, out var channels))
        {
            logger.LogDebug("Publishing to {Count} subscribers in group '{GroupId}': {MessageType}",
                channels.Count, groupId, message.GetType().Name);

            var tasks = channels.Values.Select(channel =>
                channel.Writer.WriteAsync(message).AsTask()
            );

            await Task.WhenAll(tasks);
        }
        else
        {
            logger.LogDebug("Published to group '{GroupId}', but no subscribers", groupId);
        }
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
        logger.LogDebug("Broadcasting to ALL groups: {MessageType}", message.GetType().Name);

        var allTasks = new List<Task>();

        foreach (var (groupId, channels) in _localSubscribers)
        {
            logger.LogDebug("Broadcasting to {Count} subscribers in group '{GroupId}'",
                channels.Count, groupId);

            var tasks = channels.Values.Select(channel =>
                channel.Writer.WriteAsync(message).AsTask()
            );

            allTasks.AddRange(tasks);
        }

        await Task.WhenAll(allTasks);
    }

    /// <summary>
    /// Get count of subscribers for a group.
    /// </summary>
    public int GetLocalSubscriberCount(string groupId)
    {
        return _localSubscribers.TryGetValue(groupId, out var channels) ? channels.Count : 0;
    }

    /// <summary>
    /// Get all group IDs that have active subscribers.
    /// </summary>
    public IEnumerable<string> GetLocalGroups()
    {
        return _localSubscribers.Keys;
    }

    /// <summary>
    /// Get diagnostic info about the backplane state.
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
        foreach (var groupChannels in _localSubscribers.Values)
        {
            foreach (var channel in groupChannels.Values)
            {
                channel.Writer.Complete();
            }
        }

        _localSubscribers.Clear();
        logger.LogDebug("Disposed");
    }
}
