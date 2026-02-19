using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

internal sealed class PostgresBackplane : ISseBackplane, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresBackplane> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    internal readonly Guid ServerId = Guid.NewGuid();

    private readonly ConcurrentDictionary<string, Channel<SseEvent>> _localConnections = new();
    private readonly PostgresClients _clients;
    private readonly PostgresGroups _groupsApi;

    public PostgresBackplane(NpgsqlDataSource dataSource, ILogger<PostgresBackplane> logger, JsonSerializerOptions? jsonOptions = null)
    {
        _dataSource = dataSource;
        _logger = logger;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        _clients = new PostgresClients(this);
        _groupsApi = new PostgresGroups(this);
    }

    public IBackplaneClients Clients => _clients;
    public IBackplaneGroups Groups => _groupsApi;
    public event EventHandler<ClientDisconnectedEventArgs>? OnClientDisconnected;
    public event EventHandler<GroupChangedEventArgs>? OnGroupChanged;

    public (ChannelReader<SseEvent> Reader, string ConnectionId) Connect()
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid().ToString();
        _localConnections.TryAdd(connectionId, channel);

        InsertConnectionAsync(connectionId).GetAwaiter().GetResult();

        _logger.LogDebug("Client {ConnectionId} connected on server {ServerId}", connectionId, ServerId);
        return (channel.Reader, connectionId);
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_localConnections.TryRemove(connectionId, out var channel))
            return;

        var groups = await GetClientGroupsAsync(connectionId);

        await using var cmd = _dataSource.CreateCommand(
            "DELETE FROM \"SseConnections\" WHERE \"ConnectionId\" = $1");
        cmd.Parameters.AddWithValue(connectionId);
        await cmd.ExecuteNonQueryAsync();

        channel.Writer.Complete();
        _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);

        foreach (var group in groups)
            OnGroupChanged?.Invoke(this, new GroupChangedEventArgs { ConnectionId = connectionId, GroupName = group, ChangeType = GroupChangeType.Removed });

        OnClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ConnectionId = connectionId, Groups = groups });
    }

    internal async Task AddToGroupAsync(string connectionId, string groupName)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO \"SseConnectionGroups\" (\"ConnectionId\", \"GroupName\") VALUES ($1, $2) ON CONFLICT DO NOTHING");
        cmd.Parameters.AddWithValue(connectionId);
        cmd.Parameters.AddWithValue(groupName);
        await cmd.ExecuteNonQueryAsync();

        _logger.LogDebug("Client {ConnectionId} added to group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs { ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Added });
    }

    internal async Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        await using var cmd = _dataSource.CreateCommand(
            "DELETE FROM \"SseConnectionGroups\" WHERE \"ConnectionId\" = $1 AND \"GroupName\" = $2");
        cmd.Parameters.AddWithValue(connectionId);
        cmd.Parameters.AddWithValue(groupName);
        await cmd.ExecuteNonQueryAsync();

        _logger.LogDebug("Client {ConnectionId} removed from group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs { ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Removed });
    }

    internal async Task<int> GetGroupMemberCountAsync(string groupName)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT COUNT(*) FROM \"SseConnectionGroups\" WHERE \"GroupName\" = $1");
        cmd.Parameters.AddWithValue(groupName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    internal async Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupName)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT \"ConnectionId\" FROM \"SseConnectionGroups\" WHERE \"GroupName\" = $1");
        cmd.Parameters.AddWithValue(groupName);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }

    internal async Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT \"GroupName\" FROM \"SseConnectionGroups\" WHERE \"ConnectionId\" = $1");
        cmd.Parameters.AddWithValue(connectionId);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }

    internal async Task SendToAllAsync(object data)
    {
        var json = JsonSerializer.Serialize(new MessageEnvelope
        {
            Type = MessageType.Broadcast,
            Data = JsonSerializer.SerializeToElement(data, _jsonOptions)
        }, _jsonOptions);

        await NotifyAsync("sse:broadcast", json);
        _logger.LogDebug("Broadcast message sent");
    }

    internal async Task SendToClientAsync(string connectionId, object data, string? groupName = null)
    {
        if (_localConnections.TryGetValue(connectionId, out var channel))
        {
            var evt = new SseEvent(groupName, JsonSerializer.SerializeToElement(data, _jsonOptions));
            await channel.Writer.WriteAsync(evt);
            return;
        }

        var serverId = await GetConnectionServerIdAsync(connectionId);
        if (serverId is null)
        {
            _logger.LogDebug("Client {ConnectionId} not found", connectionId);
            return;
        }

        var json = JsonSerializer.Serialize(new MessageEnvelope
        {
            Type = MessageType.Client,
            TargetId = connectionId,
            GroupName = groupName,
            Data = JsonSerializer.SerializeToElement(data, _jsonOptions)
        }, _jsonOptions);

        await NotifyAsync($"sse:server:{serverId}", json);
    }

    internal async Task SendToGroupAsync(string groupName, object data)
    {
        var members = await GetGroupMembersWithServerAsync(groupName);
        var jsonData = JsonSerializer.SerializeToElement(data, _jsonOptions);

        var byServer = members.GroupBy(m => m.ServerId);

        foreach (var group in byServer)
        {
            var connectionIds = group.Select(m => m.ConnectionId).ToList();

            if (group.Key == ServerId.ToString())
            {
                var evt = new SseEvent(groupName, jsonData);
                foreach (var id in connectionIds)
                {
                    if (_localConnections.TryGetValue(id, out var channel))
                        await channel.Writer.WriteAsync(evt);
                }
                continue;
            }

            var json = JsonSerializer.Serialize(new MessageEnvelope
            {
                Type = MessageType.Group,
                GroupName = groupName,
                TargetIds = connectionIds,
                Data = jsonData
            }, _jsonOptions);

            await NotifyAsync($"sse:server:{group.Key}", json);
        }

        _logger.LogDebug("Sent to group '{Group}' ({Count} members)", groupName, members.Count);
    }

    internal async Task DeliverLocalAsync(string connectionId, SseEvent evt)
    {
        if (_localConnections.TryGetValue(connectionId, out var channel))
            await channel.Writer.WriteAsync(evt);
    }

    internal IEnumerable<string> GetLocalConnectionIds() => _localConnections.Keys;

    internal async Task HandleServerMessageAsync(string payload)
    {
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(payload, DeserializeOptions);
        if (envelope is null) return;

        var evt = new SseEvent(envelope.GroupName, envelope.Data);

        switch (envelope.Type)
        {
            case MessageType.Client:
                if (envelope.TargetId is not null && _localConnections.TryGetValue(envelope.TargetId, out var ch))
                    await ch.Writer.WriteAsync(evt);
                break;

            case MessageType.Group:
                if (envelope.TargetIds is not null)
                {
                    foreach (var id in envelope.TargetIds)
                    {
                        if (_localConnections.TryGetValue(id, out var groupCh))
                            await groupCh.Writer.WriteAsync(evt);
                    }
                }
                break;
        }
    }

    internal async Task HandleBroadcastMessageAsync(string payload)
    {
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(payload, DeserializeOptions);
        if (envelope is null) return;

        var evt = new SseEvent(null, envelope.Data);
        foreach (var channel in _localConnections.Values)
            await channel.Writer.WriteAsync(evt);
    }

    internal async Task UpdateHeartbeatAsync(IEnumerable<string> connectionIds)
    {
        var ids = connectionIds.ToArray();
        if (ids.Length == 0) return;

        await using var cmd = _dataSource.CreateCommand(
            "UPDATE \"SseConnections\" SET \"LastSeen\" = NOW() WHERE \"ConnectionId\" = ANY($1)");
        cmd.Parameters.AddWithValue(ids);
        await cmd.ExecuteNonQueryAsync();
    }

    internal async Task DeleteStaleConnectionsAsync(TimeSpan ttl)
    {
        await using var cmd = _dataSource.CreateCommand(
            "DELETE FROM \"SseConnections\" WHERE \"LastSeen\" < NOW() - $1::interval");
        cmd.Parameters.AddWithValue(ttl.ToString());
        var deleted = await cmd.ExecuteNonQueryAsync();
        if (deleted > 0)
            _logger.LogDebug("Cleaned up {Count} stale connections", deleted);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _localConnections.Keys.ToList())
            await DisconnectAsync(id);

        _logger.LogInformation("Postgres backplane disposed. ServerId: {ServerId}", ServerId);
    }

    private async Task InsertConnectionAsync(string connectionId)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO \"SseConnections\" (\"ConnectionId\", \"ServerId\", \"LastSeen\") VALUES ($1, $2, NOW()) ON CONFLICT DO NOTHING");
        cmd.Parameters.AddWithValue(connectionId);
        cmd.Parameters.AddWithValue(ServerId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string?> GetConnectionServerIdAsync(string connectionId)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT \"ServerId\" FROM \"SseConnections\" WHERE \"ConnectionId\" = $1");
        cmd.Parameters.AddWithValue(connectionId);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    private async Task<List<(string ConnectionId, string ServerId)>> GetGroupMembersWithServerAsync(string groupName)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT g.\"ConnectionId\", c.\"ServerId\" FROM \"SseConnectionGroups\" g " +
            "JOIN \"SseConnections\" c ON c.\"ConnectionId\" = g.\"ConnectionId\" " +
            "WHERE g.\"GroupName\" = $1");
        cmd.Parameters.AddWithValue(groupName);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<(string, string)>();
        while (await reader.ReadAsync())
            result.Add((reader.GetString(0), reader.GetString(1)));
        return result;
    }

    internal async Task NotifyAsync(string channel, string payload)
    {
        await using var cmd = _dataSource.CreateCommand($"SELECT pg_notify('{channel}', $1)");
        cmd.Parameters.AddWithValue(payload);
        await cmd.ExecuteNonQueryAsync();
    }

    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    private enum MessageType { Broadcast, Client, Group }

    private sealed class MessageEnvelope
    {
        public MessageType Type { get; init; }
        public string? TargetId { get; init; }
        public List<string>? TargetIds { get; init; }
        public string? GroupName { get; init; }
        public JsonElement Data { get; init; }
    }

    private sealed class PostgresClients(PostgresBackplane backplane) : IBackplaneClients
    {
        public Task SendToAllAsync(object data) => backplane.SendToAllAsync(data);
        public Task SendToClientAsync(string connectionId, object data) => backplane.SendToClientAsync(connectionId, data);
        public async Task SendToClientsAsync(IEnumerable<string> connectionIds, object data)
        {
            foreach (var id in connectionIds)
                await backplane.SendToClientAsync(id, data);
        }
        public Task SendToGroupAsync(string groupName, object data) => backplane.SendToGroupAsync(groupName, data);
        public async Task SendToGroupsAsync(IEnumerable<string> groupNames, object data)
        {
            foreach (var g in groupNames)
                await backplane.SendToGroupAsync(g, data);
        }
    }

    private sealed class PostgresGroups(PostgresBackplane backplane) : IBackplaneGroups
    {
        public Task AddToGroupAsync(string connectionId, string groupName) => backplane.AddToGroupAsync(connectionId, groupName);
        public Task RemoveFromGroupAsync(string connectionId, string groupName) => backplane.RemoveFromGroupAsync(connectionId, groupName);
        public Task<int> GetMemberCountAsync(string groupName) => backplane.GetGroupMemberCountAsync(groupName);
        public Task<IReadOnlyList<string>> GetMembersAsync(string groupName) => backplane.GetGroupMembersAsync(groupName);
        public Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId) => backplane.GetClientGroupsAsync(connectionId);
    }
}
