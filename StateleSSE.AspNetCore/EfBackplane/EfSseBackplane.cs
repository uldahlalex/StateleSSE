using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.EfBackplane;

public class EfSseBackplane<TContext> : ISseBackplane, IDisposable where TContext : SseDbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfSseBackplane<TContext>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups = new();

    private readonly EfClients _clients;
    private readonly EfGroups _groupsApi;

    public EfSseBackplane(
        IServiceScopeFactory scopeFactory,
        ILogger<EfSseBackplane<TContext>> logger,
        JsonSerializerOptions? jsonOptions = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        _clients = new EfClients(this);
        _groupsApi = new EfGroups(this);
    }

    public IBackplaneClients Clients => _clients;
    public IBackplaneGroups Groups => _groupsApi;
    public event EventHandler<ClientDisconnectedEventArgs>? OnClientDisconnected;
    public event EventHandler<GroupChangedEventArgs>? OnGroupChanged;

    public (ChannelReader<SseEvent> Reader, string ConnectionId) Connect()
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid().ToString();
        var state = new ConnectionState(channel);
        _connections.TryAdd(connectionId, state);

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        ctx.SseConnections.Add(new SseConnection
        {
            ConnectionId = connectionId,
            ConnectedAt = DateTimeOffset.UtcNow
        });
        ctx.SaveChanges();

        _logger.LogDebug("Client {ConnectionId} connected. Total: {Count}", connectionId, _connections.Count);
        return (channel.Reader, connectionId);
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var state))
            return;

        var clientGroups = state.Groups.Keys.ToList();

        // Remove from local group state
        foreach (var groupName in clientGroups)
        {
            if (_groups.TryGetValue(groupName, out var members))
            {
                members.TryRemove(connectionId, out _);
                if (members.IsEmpty)
                    _groups.TryRemove(groupName, out _);
            }
        }

        // Remove from DB — interceptor fires, member-list subscriptions update
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();

        var groupMembers = await ctx.SseGroupMembers
            .Where(gm => gm.ConnectionId == connectionId)
            .ToListAsync();
        ctx.SseGroupMembers.RemoveRange(groupMembers);

        var connection = await ctx.SseConnections.FindAsync(connectionId);
        if (connection != null)
            ctx.SseConnections.Remove(connection);

        await ctx.SaveChangesAsync();

        state.Channel.Writer.Complete();
        _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);

        foreach (var group in clientGroups)
        {
            OnGroupChanged?.Invoke(this, new GroupChangedEventArgs
            {
                ConnectionId = connectionId, GroupName = group, ChangeType = GroupChangeType.Removed
            });
        }

        OnClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs
        {
            ConnectionId = connectionId,
            Groups = clientGroups
        });
    }

    internal async Task AddToGroupAsync(string connectionId, string groupName)
    {
        // Update local state FIRST so interceptor-triggered broadcasts include new member
        // Only track locally if this is a real connection (supports "nickname/" hack where connectionId isn't a real connection)
        if (_connections.TryGetValue(connectionId, out var state))
        {
            state.Groups.TryAdd(groupName, 0);
            var members = _groups.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
            members.TryAdd(connectionId, 0);
        }

        // Persist via EF — interceptor fires (no FK to SseConnection, so "nickname/" entries work)
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        ctx.SseGroupMembers.Add(new SseGroupMember
        {
            ConnectionId = connectionId,
            GroupName = groupName
        });
        await ctx.SaveChangesAsync();

        _logger.LogDebug("Client {ConnectionId} added to group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs
        {
            ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Added
        });
    }

    internal async Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        // Remove from DB first — interceptor fires
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        var gm = await ctx.SseGroupMembers.FindAsync(connectionId, groupName);
        if (gm != null)
        {
            ctx.SseGroupMembers.Remove(gm);
            await ctx.SaveChangesAsync();
        }

        // Then update local state
        if (_connections.TryGetValue(connectionId, out var state))
            state.Groups.TryRemove(groupName, out _);

        if (_groups.TryGetValue(groupName, out var members))
        {
            members.TryRemove(connectionId, out _);
            if (members.IsEmpty)
                _groups.TryRemove(groupName, out _);
        }

        _logger.LogDebug("Client {ConnectionId} removed from group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs
        {
            ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Removed
        });
    }

    internal async Task SendToClientAsync(string connectionId, object data, string? groupName = null)
    {
        if (_connections.TryGetValue(connectionId, out var state))
        {
            var json = JsonSerializer.SerializeToElement(data, _jsonOptions);
            await state.Channel.Writer.WriteAsync(new SseEvent(groupName, json));
        }
    }

    internal async Task SendToAllAsync(object data)
    {
        var json = JsonSerializer.SerializeToElement(data, _jsonOptions);
        var evt = new SseEvent(null, json);
        var tasks = _connections.Values.Select(s => s.Channel.Writer.WriteAsync(evt).AsTask());
        await Task.WhenAll(tasks);
    }

    internal async Task SendToGroupAsync(string groupName, object data)
    {
        if (!_groups.TryGetValue(groupName, out var members))
            return;

        var json = JsonSerializer.SerializeToElement(data, _jsonOptions);
        var evt = new SseEvent(groupName, json);
        var tasks = members.Keys
            .Where(id => _connections.ContainsKey(id))
            .Select(id => _connections[id].Channel.Writer.WriteAsync(evt).AsTask());
        await Task.WhenAll(tasks);
    }

    internal async Task<int> GetGroupMemberCountAsync(string groupName)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        return await ctx.SseGroupMembers.CountAsync(gm => gm.GroupName == groupName);
    }

    internal async Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupName)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        return await ctx.SseGroupMembers
            .Where(gm => gm.GroupName == groupName)
            .Select(gm => gm.ConnectionId)
            .ToListAsync();
    }

    internal async Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        return await ctx.SseGroupMembers
            .Where(gm => gm.ConnectionId == connectionId)
            .Select(gm => gm.GroupName)
            .ToListAsync();
    }

    public void Dispose()
    {
        foreach (var state in _connections.Values)
            state.Channel.Writer.Complete();
        _connections.Clear();
        _groups.Clear();
    }

    private sealed class ConnectionState(Channel<SseEvent> channel)
    {
        public Channel<SseEvent> Channel { get; } = channel;
        public ConcurrentDictionary<string, byte> Groups { get; } = new();
    }

    private sealed class EfClients(EfSseBackplane<TContext> backplane) : IBackplaneClients
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

    private sealed class EfGroups(EfSseBackplane<TContext> backplane) : IBackplaneGroups
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
