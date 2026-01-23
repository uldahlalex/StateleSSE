using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// In-memory implementation of ISseBackplane for single-server deployments.
/// </summary>
public class InMemoryBackplane(ILogger<InMemoryBackplane> logger) : ISseBackplane, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _channelSubscribers = new();

    /// <summary>
    /// Creates an InMemoryBackplane instance without logging.
    /// </summary>
    public InMemoryBackplane() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBackplane>.Instance)
    {
    }

    /// <inheritdoc/>
    public (ChannelReader<SseEvent> Reader, Guid ConnectionId) Connect()
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid();
        var state = new ConnectionState(channel);

        _connections.TryAdd(connectionId, state);

        logger.LogDebug("Connected {ConnectionId}. Total: {Count}", connectionId, _connections.Count);

        return (channel.Reader, connectionId);
    }

    /// <inheritdoc/>
    public bool Subscribe(Guid connectionId, string channel)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            logger.LogWarning("Subscribe failed: connection {ConnectionId} not found", connectionId);
            return false;
        }

        if (!state.Channels.TryAdd(channel, 0))
        {
            return false; // Already subscribed
        }

        var subscribers = _channelSubscribers.GetOrAdd(channel, _ => new ConcurrentDictionary<Guid, byte>());
        subscribers.TryAdd(connectionId, 0);

        logger.LogDebug("{ConnectionId} subscribed to '{Channel}'", connectionId, channel);
        return true;
    }

    /// <inheritdoc/>
    public bool Unsubscribe(Guid connectionId, string channel)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            return false;
        }

        if (!state.Channels.TryRemove(channel, out _))
        {
            return false;
        }

        if (_channelSubscribers.TryGetValue(channel, out var subscribers))
        {
            subscribers.TryRemove(connectionId, out _);
            if (subscribers.IsEmpty)
            {
                _channelSubscribers.TryRemove(channel, out _);
            }
        }

        logger.LogDebug("{ConnectionId} unsubscribed from '{Channel}'", connectionId, channel);
        return true;
    }

    /// <inheritdoc/>
    public async Task Publish(string channel, object data)
    {
        var json = JsonSerializer.SerializeToElement(data);
        var sseEvent = new SseEvent(channel, json);

        if (_channelSubscribers.TryGetValue(channel, out var subscribers))
        {
            var tasks = new List<Task>();

            foreach (var connectionId in subscribers.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var state))
                {
                    tasks.Add(state.Channel.Writer.WriteAsync(sseEvent).AsTask());
                }
            }

            await Task.WhenAll(tasks);
            logger.LogDebug("Published to '{Channel}' ({Count} subscribers)", channel, subscribers.Count);
        }
    }

    /// <inheritdoc/>
    public void Disconnect(Guid connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var state))
        {
            return;
        }

        foreach (var channel in state.Channels.Keys)
        {
            if (_channelSubscribers.TryGetValue(channel, out var subscribers))
            {
                subscribers.TryRemove(connectionId, out _);
                if (subscribers.IsEmpty)
                {
                    _channelSubscribers.TryRemove(channel, out _);
                }
            }
        }

        state.Channel.Writer.Complete();
        logger.LogDebug("Disconnected {ConnectionId}", connectionId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var state in _connections.Values)
        {
            state.Channel.Writer.Complete();
        }
        _connections.Clear();
        _channelSubscribers.Clear();
    }

    private sealed class ConnectionState(Channel<SseEvent> channel)
    {
        public Channel<SseEvent> Channel { get; } = channel;
        public ConcurrentDictionary<string, byte> Channels { get; } = new();
    }
}
