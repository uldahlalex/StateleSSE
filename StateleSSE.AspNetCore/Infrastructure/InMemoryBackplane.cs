using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// In-memory implementation of ISseBackplane for single-server deployments.
/// </summary>
public class InMemoryBackplane : ISseBackplane, IDisposable
{
    private readonly ILogger<InMemoryBackplane> _logger;
    private readonly ConcurrentDictionary<string, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups = new();

    private readonly InMemoryClients _clients;
    private readonly InMemoryGroups _groupsApi;

    /// <summary>
    /// Creates an InMemoryBackplane instance with logging.
    /// </summary>
    public InMemoryBackplane(ILogger<InMemoryBackplane> logger)
    {
        _logger = logger;
        _clients = new InMemoryClients(this);
        _groupsApi = new InMemoryGroups(this);
    }

    /// <summary>
    /// Creates an InMemoryBackplane instance without logging.
    /// </summary>
    public InMemoryBackplane() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBackplane>.Instance)
    {
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

        _connections.TryAdd(connectionId, state);

        _logger.LogDebug("Client {ConnectionId} connected. Total: {Count}", connectionId, _connections.Count);

        return (channel.Reader, connectionId);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var state))
        {
            return Task.CompletedTask;
        }

        var clientGroups = state.Groups.Keys.ToList();

        foreach (var groupName in clientGroups)
        {
            if (_groups.TryGetValue(groupName, out var members))
            {
                members.TryRemove(connectionId, out _);
                if (members.IsEmpty)
                {
                    _groups.TryRemove(groupName, out _);
                }
            }
        }

        state.Channel.Writer.Complete();
        _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);

        // Raise disconnection event
        OnClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs
        {
            ConnectionId = connectionId,
            Groups = clientGroups
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var state in _connections.Values)
        {
            state.Channel.Writer.Complete();
        }
        _connections.Clear();
        _groups.Clear();
    }

    internal async Task SendToClientAsync(string connectionId, object data, string? groupName = null)
    {
        if (_connections.TryGetValue(connectionId, out var state))
        {
            var json = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var evt = new SseEvent(groupName, json);
            await state.Channel.Writer.WriteAsync(evt);
        }
    }

    internal async Task SendToAllAsync(object data)
    {
        var json = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var evt = new SseEvent(null, json);

        var tasks = _connections.Values
            .Select(state => state.Channel.Writer.WriteAsync(evt).AsTask());

        await Task.WhenAll(tasks);
        _logger.LogDebug("Sent to all clients ({Count})", _connections.Count);
    }

    internal async Task SendToGroupAsync(string groupName, object data)
    {
        if (!_groups.TryGetValue(groupName, out var members))
        {
            return;
        }

        var json = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var evt = new SseEvent(groupName, json);

        var tasks = members.Keys
            .Where(id => _connections.ContainsKey(id))
            .Select(id => _connections[id].Channel.Writer.WriteAsync(evt).AsTask());

        await Task.WhenAll(tasks);
        _logger.LogDebug("Sent to group '{Group}' ({Count} members)", groupName, members.Count);
    }

    internal Task AddToGroupAsync(string connectionId, string groupName)
    {
        if (!_connections.TryGetValue(connectionId, out var state))
        {
            _logger.LogWarning("AddToGroup failed: client {ConnectionId} not found", connectionId);
            return Task.CompletedTask;
        }

        state.Groups.TryAdd(groupName, 0);

        var members = _groups.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
        members.TryAdd(connectionId, 0);

        _logger.LogDebug("Client {ConnectionId} added to group '{Group}'", connectionId, groupName);
        return Task.CompletedTask;
    }

    internal Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        if (_connections.TryGetValue(connectionId, out var state))
        {
            state.Groups.TryRemove(groupName, out _);
        }

        if (_groups.TryGetValue(groupName, out var members))
        {
            members.TryRemove(connectionId, out _);
            if (members.IsEmpty)
            {
                _groups.TryRemove(groupName, out _);
            }
        }

        _logger.LogDebug("Client {ConnectionId} removed from group '{Group}'", connectionId, groupName);
        return Task.CompletedTask;
    }

    internal Task<int> GetGroupMemberCountAsync(string groupName)
    {
        var count = _groups.TryGetValue(groupName, out var members) ? members.Count : 0;
        return Task.FromResult(count);
    }

    internal Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupName)
    {
        IReadOnlyList<string> result = _groups.TryGetValue(groupName, out var members)
            ? members.Keys.ToList()
            : Array.Empty<string>();
        return Task.FromResult(result);
    }

    internal Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId)
    {
        IReadOnlyList<string> result = _connections.TryGetValue(connectionId, out var state)
            ? state.Groups.Keys.ToList()
            : Array.Empty<string>();
        return Task.FromResult(result);
    }

    private sealed class ConnectionState(Channel<SseEvent> channel)
    {
        public Channel<SseEvent> Channel { get; } = channel;
        public ConcurrentDictionary<string, byte> Groups { get; } = new();
    }

    private sealed class InMemoryClients(InMemoryBackplane backplane) : IBackplaneClients
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

    private sealed class InMemoryGroups(InMemoryBackplane backplane) : IBackplaneGroups
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
