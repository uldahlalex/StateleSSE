using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// Redis-based implementation of ISseBackplane for horizontal scaling.
/// Uses Redis as source of truth for connection and group state.
/// Implements TTL-based heartbeat for automatic cleanup of stale connections.
/// </summary>
public class RedisBackplane : ISseBackplane, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISubscriber _subscriber;
    private readonly IDatabase _db;
    private readonly string _prefix;
    private readonly ILogger<RedisBackplane> _logger;
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly TimeSpan _connectionTtl;
    private readonly TimeSpan _heartbeatInterval;

    private readonly ConcurrentDictionary<string, ConnectionState> _localConnections = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _heartbeatTask;

    private readonly RedisClients _clients;
    private readonly RedisGroups _groupsApi;

    /// <summary>
    /// Creates a RedisBackplane instance.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="prefix">Prefix for all Redis keys (default: "sse")</param>
    /// <param name="connectionTtl">TTL for connection keys - connections not refreshed within this time are considered dead (default: 60s)</param>
    public RedisBackplane(
        IConnectionMultiplexer redis,
        ILogger<RedisBackplane> logger,
        string prefix = "sse",
        TimeSpan? connectionTtl = null)
    {
        _redis = redis;
        _subscriber = redis.GetSubscriber();
        _db = redis.GetDatabase();
        _prefix = prefix;
        _logger = logger;
        _connectionTtl = connectionTtl ?? TimeSpan.FromSeconds(60);
        _heartbeatInterval = TimeSpan.FromSeconds(_connectionTtl.TotalSeconds / 3);

        _clients = new RedisClients(this);
        _groupsApi = new RedisGroups(this);

        // Subscribe to messages for this server
        _subscriber.Subscribe(
            new RedisChannel($"{_prefix}:server:{_serverId}", RedisChannel.PatternMode.Literal),
            async (_, message) => await OnServerMessage(message!)
        );

        // Subscribe to broadcast messages
        _subscriber.Subscribe(
            new RedisChannel($"{_prefix}:broadcast", RedisChannel.PatternMode.Literal),
            async (_, message) => await OnBroadcastMessage(message!)
        );

        // Start heartbeat task
        _heartbeatTask = RunHeartbeatAsync(_shutdownCts.Token);

        _logger.LogInformation(
            "Redis backplane initialized. ServerId: {ServerId}, Prefix: {Prefix}, TTL: {TTL}s",
            _serverId, _prefix, _connectionTtl.TotalSeconds);
    }

    /// <inheritdoc/>
    public IBackplaneClients Clients => _clients;

    /// <inheritdoc/>
    public IBackplaneGroups Groups => _groupsApi;

    /// <inheritdoc/>
    public event EventHandler<ClientDisconnectedEventArgs>? OnClientDisconnected;

    /// <inheritdoc/>
    public (ChannelReader<SseEvent> Reader, string ConnectionId) Connect()
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid().ToString();
        var state = new ConnectionState(channel);

        _localConnections.TryAdd(connectionId, state);

        // Register connection in Redis with TTL
        var connectionKey = $"{_prefix}:conn:{connectionId}";
        _db.HashSet(connectionKey, new[]
        {
            new HashEntry("server", _serverId.ToString()),
            new HashEntry("created", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        });
        _db.KeyExpire(connectionKey, _connectionTtl);

        // Add to server's connection set
        _db.SetAdd($"{_prefix}:server:{_serverId}:connections", connectionId);

        _logger.LogDebug("Client {ConnectionId} connected on server {ServerId}", connectionId, _serverId);

        return (channel.Reader, connectionId);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(string connectionId)
    {
        // Remove from local state
        if (!_localConnections.TryRemove(connectionId, out var state))
        {
            return;
        }

        // Get groups before removing connection
        var groups = await _db.SetMembersAsync($"{_prefix}:conn:{connectionId}:groups");
        var clientGroups = groups.Select(g => g.ToString()).ToList();

        // Remove from all groups in Redis
        foreach (var group in groups)
        {
            await _db.SetRemoveAsync($"{_prefix}:group:{group}:members", connectionId.ToString());
        }

        // Remove connection data from Redis
        await _db.KeyDeleteAsync(new RedisKey[]
        {
            $"{_prefix}:conn:{connectionId}",
            $"{_prefix}:conn:{connectionId}:groups"
        });

        // Remove from server's connection set
        await _db.SetRemoveAsync($"{_prefix}:server:{_serverId}:connections", connectionId);

        state.Channel.Writer.Complete();
        _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);

        // Raise disconnection event
        OnClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs
        {
            ConnectionId = connectionId,
            Groups = clientGroups
        });
    }

    internal async Task AddToGroupAsync(string connectionId, string groupName)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            _logger.LogWarning("Attempted to add null/empty connectionId to group '{Group}'", groupName);
            return;
        }

        // Add connection to group members
        await _db.SetAddAsync($"{_prefix}:group:{groupName}:members", connectionId);

        // Track which groups this connection belongs to
        await _db.SetAddAsync($"{_prefix}:conn:{connectionId}:groups", groupName);

        // Also track locally for faster message routing
        if (_localConnections.TryGetValue(connectionId, out var state))
        {
            state.Groups.TryAdd(groupName, 0);
        }

        _logger.LogDebug("Client {ConnectionId} added to group '{Group}'", connectionId, groupName);
    }

    internal async Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        await _db.SetRemoveAsync($"{_prefix}:group:{groupName}:members", connectionId);
        await _db.SetRemoveAsync($"{_prefix}:conn:{connectionId}:groups", groupName);

        if (_localConnections.TryGetValue(connectionId, out var state))
        {
            state.Groups.TryRemove(groupName, out _);
        }

        _logger.LogDebug("Client {ConnectionId} removed from group '{Group}'", connectionId, groupName);
    }

    internal async Task<int> GetGroupMemberCountAsync(string groupName)
    {
        // Get validated members count (cleans up stale connections)
        var members = await GetGroupMembersAsync(groupName, validateConnections: true);
        return members.Count;
    }

    internal async Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupName, bool validateConnections = true)
    {
        var members = await _db.SetMembersAsync($"{_prefix}:group:{groupName}:members");
        var parsed = members
            .Select(m => m.ToString())
            .Where(m => !string.IsNullOrEmpty(m))
            .ToList();

        if (!validateConnections)
            return parsed;

        // Validate each connection still exists and clean up stale ones
        var validMembers = new List<string>();
        var staleMembers = new List<string>();

        foreach (var connectionId in parsed)
        {
            var exists = await _db.KeyExistsAsync($"{_prefix}:conn:{connectionId}");
            if (exists)
                validMembers.Add(connectionId);
            else
                staleMembers.Add(connectionId);
        }

        // Clean up stale members in background
        if (staleMembers.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var stale in staleMembers)
                {
                    await _db.SetRemoveAsync($"{_prefix}:group:{groupName}:members", stale);
                    _logger.LogDebug("Removed stale connection {ConnectionId} from group '{Group}'", stale, groupName);
                }
            });
        }

        return validMembers;
    }

    internal async Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId)
    {
        var groups = await _db.SetMembersAsync($"{_prefix}:conn:{connectionId}:groups");
        return groups.Select(g => g.ToString()).ToList();
    }

    internal async Task SendToAllAsync(object data)
    {
        var json = JsonSerializer.Serialize(new MessageEnvelope
        {
            Type = MessageType.Broadcast,
            Data = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        },new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _subscriber.PublishAsync(new RedisChannel($"{_prefix}:broadcast", RedisChannel.PatternMode.Literal), json);
        _logger.LogDebug("Broadcast message sent");
    }

    internal async Task SendToClientAsync(string connectionId, object data, string? groupName = null)
    {
        // Check if connection is local
        if (_localConnections.TryGetValue(connectionId, out var state))
        {
            var evt = new SseEvent(groupName, JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            await state.Channel.Writer.WriteAsync(evt);
            return;
        }

        // Find which server owns this connection
        var serverId = await _db.HashGetAsync($"{_prefix}:conn:{connectionId}", "server");
        if (serverId.IsNullOrEmpty)
        {
            _logger.LogDebug("Client {ConnectionId} not found", connectionId);
            return;
        }

        // Send to that server
        var json = JsonSerializer.Serialize(new MessageEnvelope
        {
            Type = MessageType.Client,
            TargetId = connectionId,
            GroupName = groupName,
            Data = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        }, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _subscriber.PublishAsync(new RedisChannel($"{_prefix}:server:{serverId}", RedisChannel.PatternMode.Literal), json);
    }

    internal async Task SendToGroupAsync(string groupName, object data)
    {
        // Get all members of the group
        var members = await _db.SetMembersAsync($"{_prefix}:group:{groupName}:members");

        // Group members by server for efficient routing
        var serverConnections = new Dictionary<string, List<string>>();
        var staleConnections = new List<string>();

        foreach (var member in members)
        {
            var connectionId = member.ToString();
            if (string.IsNullOrEmpty(connectionId))
            {
                staleConnections.Add(connectionId);
                continue;
            }

            var serverId = await _db.HashGetAsync($"{_prefix}:conn:{connectionId}", "server");

            if (serverId.IsNullOrEmpty)
            {
                staleConnections.Add(connectionId);
                continue;
            }

            var serverIdStr = serverId.ToString();
            if (!serverConnections.ContainsKey(serverIdStr))
            {
                serverConnections[serverIdStr] = new List<string>();
            }
            serverConnections[serverIdStr].Add(connectionId);
        }

        // Clean up stale connections in background
        if (staleConnections.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var stale in staleConnections)
                {
                    await _db.SetRemoveAsync($"{_prefix}:group:{groupName}:members", stale);
                }
                _logger.LogDebug("Cleaned up {Count} stale connections from group '{Group}'", staleConnections.Count, groupName);
            });
        }

        var jsonData = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Send to each server
        foreach (var (serverId, connections) in serverConnections)
        {
            if (serverId == _serverId.ToString())
            {
                // Local delivery
                var evt = new SseEvent(groupName, jsonData);
                foreach (var connectionId in connections)
                {
                    if (_localConnections.TryGetValue(connectionId, out var state))
                    {
                        await state.Channel.Writer.WriteAsync(evt);
                    }
                }
            }
            else
            {
                // Remote delivery
                var json = JsonSerializer.Serialize(new MessageEnvelope
                {
                    Type = MessageType.Group,
                    GroupName = groupName,
                    TargetIds = connections,
                    Data = jsonData
                }, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await _subscriber.PublishAsync(new RedisChannel($"{_prefix}:server:{serverId}", RedisChannel.PatternMode.Literal), json);
            }
        }

        _logger.LogDebug("Sent to group '{Group}' ({Count} members)", groupName, members.Length);
    }

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task OnServerMessage(RedisValue message)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(message.ToString(), DeserializeOptions);
            if (envelope == null) return;

            var evt = new SseEvent(envelope.GroupName, envelope.Data);

            switch (envelope.Type)
            {
                case MessageType.Client:
                    if (envelope.TargetId != null && _localConnections.TryGetValue(envelope.TargetId, out var state))
                    {
                        await state.Channel.Writer.WriteAsync(evt);
                    }
                    break;

                case MessageType.Group:
                    if (envelope.TargetIds != null)
                    {
                        foreach (var connectionId in envelope.TargetIds)
                        {
                            if (_localConnections.TryGetValue(connectionId, out var connState))
                            {
                                await connState.Channel.Writer.WriteAsync(evt);
                            }
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing server message");
        }
    }

    private async Task OnBroadcastMessage(RedisValue message)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(message.ToString(), DeserializeOptions);
            if (envelope == null) return;

            var evt = new SseEvent(null, envelope.Data);

            foreach (var state in _localConnections.Values)
            {
                await state.Channel.Writer.WriteAsync(evt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing broadcast message");
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                foreach (var connectionId in _localConnections.Keys)
                {
                    var key = $"{_prefix}:conn:{connectionId}";
                    await _db.KeyExpireAsync(key, _connectionTtl);
                }

                _logger.LogTrace("Heartbeat: refreshed TTL for {Count} connections", _localConnections.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();

        try { await _heartbeatTask; } catch (OperationCanceledException) { }

        // Clean up all local connections
        foreach (var connectionId in _localConnections.Keys.ToList())
        {
            await DisconnectAsync(connectionId);
        }

        // Remove server's connection set
        await _db.KeyDeleteAsync($"{_prefix}:server:{_serverId}:connections");

        _subscriber.Unsubscribe(new RedisChannel($"{_prefix}:server:{_serverId}", RedisChannel.PatternMode.Literal));
        _subscriber.Unsubscribe(new RedisChannel($"{_prefix}:broadcast", RedisChannel.PatternMode.Literal));

        _shutdownCts.Dispose();

        _logger.LogInformation("Redis backplane disposed. ServerId: {ServerId}", _serverId);
    }

    private sealed class ConnectionState(Channel<SseEvent> channel)
    {
        public Channel<SseEvent> Channel { get; } = channel;
        public ConcurrentDictionary<string, byte> Groups { get; } = new();
    }

    private enum MessageType { Broadcast, Client, Group }

    private sealed class MessageEnvelope
    {
        public MessageType Type { get; init; }
        public string? TargetId { get; init; }
        public List<string>? TargetIds { get; init; }
        public string? GroupName { get; init; }
        public JsonElement Data { get; init; }
    }

    private sealed class RedisClients(RedisBackplane backplane) : IBackplaneClients
    {
        public Task SendToAllAsync(object data) => backplane.SendToAllAsync(data);

        public Task SendToClientAsync(string connectionId, object data) =>
            backplane.SendToClientAsync(connectionId, data);

        public async Task SendToClientsAsync(IEnumerable<string> connectionIds, object data)
        {
            var tasks = connectionIds.Select(id => backplane.SendToClientAsync(id, data));
            await Task.WhenAll(tasks);
        }

        public Task SendToGroupAsync(string groupName, object data) =>
            backplane.SendToGroupAsync(groupName, data);

        public async Task SendToGroupsAsync(IEnumerable<string> groupNames, object data)
        {
            var tasks = groupNames.Select(g => backplane.SendToGroupAsync(g, data));
            await Task.WhenAll(tasks);
        }
    }

    private sealed class RedisGroups(RedisBackplane backplane) : IBackplaneGroups
    {
        public Task AddToGroupAsync(string connectionId, string groupName) =>
            backplane.AddToGroupAsync(connectionId, groupName);

        public Task RemoveFromGroupAsync(string connectionId, string groupName) =>
            backplane.RemoveFromGroupAsync(connectionId, groupName);

        public Task<int> GetMemberCountAsync(string groupName) =>
            backplane.GetGroupMemberCountAsync(groupName);

        public Task<IReadOnlyList<string>> GetMembersAsync(string groupName) =>
            backplane.GetGroupMembersAsync(groupName);

        public Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId) =>
            backplane.GetClientGroupsAsync(connectionId);
    }
}
