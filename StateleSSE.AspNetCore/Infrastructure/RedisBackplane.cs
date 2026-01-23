using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// Redis-based implementation of ISseBackplane for horizontal scaling.
/// </summary>
public class RedisBackplane : ISseBackplane, IDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly string _channelPrefix;
    private readonly ILogger<RedisBackplane> _logger;

    private readonly ConcurrentDictionary<Guid, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _channelSubscribers = new();

    public RedisBackplane(IConnectionMultiplexer redis, ILogger<RedisBackplane> logger, string channelPrefix = "sse")
    {
        _subscriber = redis.GetSubscriber();
        _channelPrefix = channelPrefix;
        _logger = logger;

        _subscriber.Subscribe(
            new RedisChannel($"{_channelPrefix}:events", RedisChannel.PatternMode.Literal),
            async (_, message) =>
            {
                try
                {
                    await OnRedisMessage(message!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Redis message");
                }
            }
        );

        _logger.LogInformation("Redis backplane initialized with prefix '{Prefix}'", _channelPrefix);
    }

    /// <inheritdoc/>
    public (ChannelReader<SseEvent> Reader, Guid ConnectionId) Connect()
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid();
        var state = new ConnectionState(channel);

        _connections.TryAdd(connectionId, state);

        _logger.LogDebug("Connected {ConnectionId}. Total local: {Count}", connectionId, _connections.Count);

        return (channel.Reader, connectionId);
    }

    /// <inheritdoc/>
    public bool Subscribe(Guid connectionId, string channel)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            _logger.LogWarning("Subscribe failed: connection {ConnectionId} not found", connectionId);
            return false;
        }

        if (!state.Channels.TryAdd(channel, 0))
        {
            return false;
        }

        var subscribers = _channelSubscribers.GetOrAdd(channel, _ => new ConcurrentDictionary<Guid, byte>());
        subscribers.TryAdd(connectionId, 0);

        _logger.LogDebug("{ConnectionId} subscribed to '{Channel}'", connectionId, channel);
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

        _logger.LogDebug("{ConnectionId} unsubscribed from '{Channel}'", connectionId, channel);
        return true;
    }

    /// <inheritdoc/>
    public async Task Publish(string channel, object data)
    {
        var json = JsonSerializer.SerializeToElement(data);
        var envelope = new RedisEnvelope { Channel = channel, Data = json };
        var message = JsonSerializer.Serialize(envelope);

        await _subscriber.PublishAsync(
            new RedisChannel($"{_channelPrefix}:events", RedisChannel.PatternMode.Literal),
            message
        );

        _logger.LogDebug("Published to Redis channel '{Channel}'", channel);
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
        _logger.LogDebug("Disconnected {ConnectionId}", connectionId);
    }

    private async Task OnRedisMessage(RedisValue message)
    {
        var envelope = JsonSerializer.Deserialize<RedisEnvelope>(message.ToString());
        if (envelope == null) return;

        var sseEvent = new SseEvent(envelope.Channel, envelope.Data);

        if (_channelSubscribers.TryGetValue(envelope.Channel, out var subscribers))
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
            _logger.LogDebug("Forwarded to {Count} local subscribers for '{Channel}'",
                subscribers.Count, envelope.Channel);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriber.Unsubscribe(new RedisChannel($"{_channelPrefix}:events", RedisChannel.PatternMode.Literal));

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

    private sealed class RedisEnvelope
    {
        public required string Channel { get; init; }
        public required JsonElement Data { get; init; }
    }
}
