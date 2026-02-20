using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure.EfBackplane;

internal sealed class EfBackplane<TDbContext> : ISseBackplane, IDisposable
    where TDbContext : DbContext, ISseEfContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfBackplane<TDbContext>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private readonly ConcurrentDictionary<string, Channel<SseEvent>> _channels = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups = new();

    private readonly EfClients _clients;
    private readonly EfGroups _groupsApi;

    public EfBackplane(IServiceScopeFactory scopeFactory, ILogger<EfBackplane<TDbContext>> logger, JsonSerializerOptions? jsonOptions = null)
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

    public (ChannelReader<SseEvent> Reader, string ConnectionId) Connect(string? ownerId = null)
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid().ToString();
        _channels.TryAdd(connectionId, channel);

        InsertConnectionAsync(connectionId, ownerId).GetAwaiter().GetResult();

        _logger.LogDebug("Client {ConnectionId} connected", connectionId);
        return (channel.Reader, connectionId);
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_channels.TryRemove(connectionId, out var channel))
            return;

        var groups = _groups
            .Where(kv => kv.Value.ContainsKey(connectionId))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var g in groups)
        {
            _groups[g].TryRemove(connectionId, out _);
            if (_groups[g].IsEmpty) _groups.TryRemove(g, out _);
        }

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var groupRows = await ctx.SseConnectionGroups
            .Where(g => g.ConnectionId == connectionId)
            .ToListAsync();
        if (groupRows.Count > 0)
        {
            ctx.SseConnectionGroups.RemoveRange(groupRows);
            await ctx.SaveChangesAsync();
        }

        var conn = await ctx.SseConnections.FindAsync(connectionId);
        if (conn is not null)
        {
            ctx.SseConnections.Remove(conn);
            await ctx.SaveChangesAsync();
        }

        channel.Writer.Complete();
        _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);

        foreach (var g in groups)
            OnGroupChanged?.Invoke(this, new GroupChangedEventArgs { ConnectionId = connectionId, GroupName = g, ChangeType = GroupChangeType.Removed });

        OnClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ConnectionId = connectionId, Groups = groups });
    }

    internal async Task AddToGroupAsync(string connectionId, string groupName)
    {
        var members = _groups.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
        members.TryAdd(connectionId, 0);

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TDbContext>();
        if (!await ctx.SseConnectionGroups.AnyAsync(g => g.ConnectionId == connectionId && g.GroupName == groupName))
        {
            ctx.SseConnectionGroups.Add(new SseConnectionGroup { ConnectionId = connectionId, GroupName = groupName });
            await ctx.SaveChangesAsync();
        }

        _logger.LogDebug("Client {ConnectionId} added to group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs { ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Added });
    }

    internal async Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        if (_groups.TryGetValue(groupName, out var members))
        {
            members.TryRemove(connectionId, out _);
            if (members.IsEmpty) _groups.TryRemove(groupName, out _);
        }

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var row = await ctx.SseConnectionGroups.FindAsync(connectionId, groupName);
        if (row is not null)
        {
            ctx.SseConnectionGroups.Remove(row);
            await ctx.SaveChangesAsync();
        }

        _logger.LogDebug("Client {ConnectionId} removed from group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs { ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Removed });
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
            : [];
        return Task.FromResult(result);
    }

    internal Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId)
    {
        IReadOnlyList<string> result = _groups
            .Where(kv => kv.Value.ContainsKey(connectionId))
            .Select(kv => kv.Key)
            .ToList();
        return Task.FromResult(result);
    }

    internal async Task SendToGroupAsync(string groupName, object data)
    {
        if (!_groups.TryGetValue(groupName, out var members))
            return;

        var json = JsonSerializer.SerializeToElement(data, _jsonOptions);
        var evt = new SseEvent(groupName, json);

        foreach (var id in members.Keys)
        {
            if (_channels.TryGetValue(id, out var ch))
                await ch.Writer.WriteAsync(evt);
        }

        _logger.LogDebug("Sent to group '{Group}' ({Count} members)", groupName, members.Count);
    }

    internal async Task SendToClientAsync(string connectionId, object data, string? groupName = null)
    {
        if (!_channels.TryGetValue(connectionId, out var channel))
            return;

        var evt = new SseEvent(groupName, JsonSerializer.SerializeToElement(data, _jsonOptions));
        await channel.Writer.WriteAsync(evt);
    }

    internal async Task SendToAllAsync(object data)
    {
        var json = JsonSerializer.SerializeToElement(data, _jsonOptions);
        var evt = new SseEvent(null, json);

        foreach (var ch in _channels.Values)
            await ch.Writer.WriteAsync(evt);

        _logger.LogDebug("Sent to all ({Count} clients)", _channels.Count);
    }

    private async Task InsertConnectionAsync(string connectionId, string? ownerId)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TDbContext>();
        ctx.SseConnections.Add(new SseConnection
        {
            ConnectionId = connectionId,
            ServerId = "local",
            LastSeen = DateTimeOffset.UtcNow,
            OwnerId = ownerId
        });
        await ctx.SaveChangesAsync();
    }

    public void Dispose()
    {
        foreach (var ch in _channels.Values)
            ch.Writer.Complete();
        _channels.Clear();
        _groups.Clear();
    }

    private sealed class EfClients(EfBackplane<TDbContext> bp) : IBackplaneClients
    {
        public Task SendToAllAsync(object data) => bp.SendToAllAsync(data);
        public Task SendToClientAsync(string connectionId, object data) => bp.SendToClientAsync(connectionId, data);
        public async Task SendToClientsAsync(IEnumerable<string> connectionIds, object data)
        {
            foreach (var id in connectionIds) await bp.SendToClientAsync(id, data);
        }
        public Task SendToGroupAsync(string groupName, object data) => bp.SendToGroupAsync(groupName, data);
        public async Task SendToGroupsAsync(IEnumerable<string> groupNames, object data)
        {
            foreach (var g in groupNames) await bp.SendToGroupAsync(g, data);
        }
    }

    private sealed class EfGroups(EfBackplane<TDbContext> bp) : IBackplaneGroups
    {
        public Task AddToGroupAsync(string connectionId, string groupName) => bp.AddToGroupAsync(connectionId, groupName);
        public Task RemoveFromGroupAsync(string connectionId, string groupName) => bp.RemoveFromGroupAsync(connectionId, groupName);
        public Task<int> GetMemberCountAsync(string groupName) => bp.GetGroupMemberCountAsync(groupName);
        public Task<IReadOnlyList<string>> GetMembersAsync(string groupName) => bp.GetGroupMembersAsync(groupName);
        public Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId) => bp.GetClientGroupsAsync(connectionId);
    }
}
