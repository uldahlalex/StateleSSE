using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace StateleSSE.AspNetCore.Infrastructure;

public class PostgresBackplane : ISseBackplane, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresBackplane> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _serverId = Guid.NewGuid().ToString();
    private readonly TimeSpan _connectionTtl;
    private readonly TimeSpan _heartbeatInterval;
    private const int NotifyMaxBytes = 7000;

    private readonly ConcurrentDictionary<string, ConnectionState> _localConnections = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _heartbeatTask;
    private readonly Task _listenTask;

    private readonly PostgresClients _clients;
    private readonly PostgresGroups _groupsApi;

    public PostgresBackplane(
        string connectionString,
        ILogger<PostgresBackplane> logger,
        TimeSpan? connectionTtl = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        _connectionString = connectionString;
        _logger = logger;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        _connectionTtl = connectionTtl ?? TimeSpan.FromSeconds(60);
        _heartbeatInterval = TimeSpan.FromSeconds(_connectionTtl.TotalSeconds / 3);

        _clients = new PostgresClients(this);
        _groupsApi = new PostgresGroups(this);

        EnsureTables();

        _listenTask = RunListenAsync(_shutdownCts.Token);
        _heartbeatTask = RunHeartbeatAsync(_shutdownCts.Token);

        _logger.LogInformation(
            "Postgres backplane initialized. ServerId: {ServerId}, TTL: {TTL}s",
            _serverId, _connectionTtl.TotalSeconds);
    }

    public IBackplaneClients Clients => _clients;
    public IBackplaneGroups Groups => _groupsApi;
    public event EventHandler<ClientDisconnectedEventArgs>? OnClientDisconnected;
    public event EventHandler<GroupChangedEventArgs>? OnGroupChanged;

    private void EnsureTables()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE UNLOGGED TABLE IF NOT EXISTS sse_connections (
                connection_id  TEXT PRIMARY KEY,
                server_id      TEXT NOT NULL,
                last_heartbeat TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS idx_sse_conn_server ON sse_connections (server_id);

            CREATE UNLOGGED TABLE IF NOT EXISTS sse_group_members (
                connection_id  TEXT NOT NULL,
                group_name     TEXT NOT NULL,
                PRIMARY KEY (connection_id, group_name)
            );
            CREATE INDEX IF NOT EXISTS idx_sse_gm_group ON sse_group_members (group_name);

            CREATE UNLOGGED TABLE IF NOT EXISTS sse_messages (
                id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                payload    TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public (ChannelReader<SseEvent> Reader, string ConnectionId) Connect(string? userId = null)
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        var connectionId = Guid.NewGuid().ToString();
        var state = new ConnectionState(channel);

        _localConnections.TryAdd(connectionId, state);

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sse_connections (connection_id, server_id) VALUES ($1, $2)";
        cmd.Parameters.AddWithValue(connectionId);
        cmd.Parameters.AddWithValue(_serverId);
        cmd.ExecuteNonQuery();

        _logger.LogDebug("Client {ConnectionId} connected on server {ServerId}", connectionId, _serverId);
        return (channel.Reader, connectionId);
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_localConnections.TryRemove(connectionId, out var state))
            return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Get groups before removing
        var clientGroups = new List<string>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT group_name FROM sse_group_members WHERE connection_id = $1";
            cmd.Parameters.AddWithValue(connectionId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                clientGroups.Add(reader.GetString(0));
        }

        // Delete group memberships and connection
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                DELETE FROM sse_group_members WHERE connection_id = $1;
                DELETE FROM sse_connections WHERE connection_id = $1;
                """;
            cmd.Parameters.AddWithValue(connectionId);
            await cmd.ExecuteNonQueryAsync();
        }

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
        if (string.IsNullOrEmpty(connectionId))
        {
            _logger.LogWarning("Attempted to add null/empty connectionId to group '{Group}'", groupName);
            return;
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sse_group_members (connection_id, group_name) VALUES ($1, $2) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue(connectionId);
        cmd.Parameters.AddWithValue(groupName);
        await cmd.ExecuteNonQueryAsync();

        if (_localConnections.TryGetValue(connectionId, out var state))
            state.Groups.TryAdd(groupName, 0);

        _logger.LogDebug("Client {ConnectionId} added to group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs
        {
            ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Added
        });
    }

    internal async Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sse_group_members WHERE connection_id = $1 AND group_name = $2";
        cmd.Parameters.AddWithValue(connectionId);
        cmd.Parameters.AddWithValue(groupName);
        await cmd.ExecuteNonQueryAsync();

        if (_localConnections.TryGetValue(connectionId, out var state))
            state.Groups.TryRemove(groupName, out _);

        _logger.LogDebug("Client {ConnectionId} removed from group '{Group}'", connectionId, groupName);
        OnGroupChanged?.Invoke(this, new GroupChangedEventArgs
        {
            ConnectionId = connectionId, GroupName = groupName, ChangeType = GroupChangeType.Removed
        });
    }

    internal async Task<int> GetGroupMemberCountAsync(string groupName)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM sse_group_members gm
            JOIN sse_connections c ON c.connection_id = gm.connection_id
            WHERE gm.group_name = $1
            """;
        cmd.Parameters.AddWithValue(groupName);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    internal async Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupName)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT gm.connection_id FROM sse_group_members gm
            JOIN sse_connections c ON c.connection_id = gm.connection_id
            WHERE gm.group_name = $1
            """;
        cmd.Parameters.AddWithValue(groupName);
        var members = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            members.Add(reader.GetString(0));
        return members;
    }

    internal async Task<IReadOnlyList<string>> GetClientGroupsAsync(string connectionId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT group_name FROM sse_group_members WHERE connection_id = $1";
        cmd.Parameters.AddWithValue(connectionId);
        var groups = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            groups.Add(reader.GetString(0));
        return groups;
    }

    internal async Task SendToAllAsync(object data)
    {
        var envelope = new MessageEnvelope
        {
            Type = MessageType.Broadcast,
            Data = JsonSerializer.SerializeToElement(data, _jsonOptions)
        };
        await PublishAsync("sse_broadcast", envelope);
        _logger.LogDebug("Broadcast message sent");
    }

    internal async Task SendToClientAsync(string connectionId, object data, string? groupName = null)
    {
        // Local delivery
        if (_localConnections.TryGetValue(connectionId, out var state))
        {
            var evt = new SseEvent(groupName, JsonSerializer.SerializeToElement(data, _jsonOptions));
            await state.Channel.Writer.WriteAsync(evt);
            return;
        }

        // Find owning server
        string? serverId;
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT server_id FROM sse_connections WHERE connection_id = $1";
            cmd.Parameters.AddWithValue(connectionId);
            var result = await cmd.ExecuteScalarAsync();
            serverId = result as string;
        }

        if (serverId == null)
        {
            _logger.LogDebug("Client {ConnectionId} not found", connectionId);
            return;
        }

        var envelope = new MessageEnvelope
        {
            Type = MessageType.Client,
            TargetId = connectionId,
            GroupName = groupName,
            Data = JsonSerializer.SerializeToElement(data, _jsonOptions)
        };
        await PublishAsync($"sse_server_{serverId}", envelope);
    }

    internal async Task SendToGroupAsync(string groupName, object data)
    {
        // Get all members with their server IDs in one query
        var serverConnections = new Dictionary<string, List<string>>();

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT gm.connection_id, c.server_id FROM sse_group_members gm
                JOIN sse_connections c ON c.connection_id = gm.connection_id
                WHERE gm.group_name = $1
                """;
            cmd.Parameters.AddWithValue(groupName);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var connId = reader.GetString(0);
                var srvId = reader.GetString(1);
                if (!serverConnections.TryGetValue(srvId, out var list))
                {
                    list = new List<string>();
                    serverConnections[srvId] = list;
                }
                list.Add(connId);
            }
        }

        var jsonData = JsonSerializer.SerializeToElement(data, _jsonOptions);

        foreach (var (serverId, connections) in serverConnections)
        {
            if (serverId == _serverId)
            {
                // Local delivery
                var evt = new SseEvent(groupName, jsonData);
                foreach (var connectionId in connections)
                {
                    if (_localConnections.TryGetValue(connectionId, out var state))
                        await state.Channel.Writer.WriteAsync(evt);
                }
            }
            else
            {
                // Remote delivery
                var envelope = new MessageEnvelope
                {
                    Type = MessageType.Group,
                    GroupName = groupName,
                    TargetIds = connections,
                    Data = jsonData
                };
                await PublishAsync($"sse_server_{serverId}", envelope);
            }
        }

        _logger.LogDebug("Sent to group '{Group}'", groupName);
    }

    private async Task PublishAsync(string channel, MessageEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        var payload = json;

        // If payload exceeds NOTIFY limit, store in overflow table
        if (System.Text.Encoding.UTF8.GetByteCount(json) > NotifyMaxBytes)
        {
            var id = Guid.NewGuid();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO sse_messages (id, payload) VALUES ($1, $2)";
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(json);
            await cmd.ExecuteNonQueryAsync();
            payload = JsonSerializer.Serialize(new { r = id.ToString() }, _jsonOptions);
        }

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"NOTIFY \"{channel}\", '{EscapeNotifyPayload(payload)}'";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string EscapeNotifyPayload(string payload) =>
        payload.Replace("'", "''");

    private async Task RunListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                conn.Notification += async (_, e) =>
                {
                    try
                    {
                        await HandleNotification(e.Channel, e.Payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling notification on channel {Channel}", e.Channel);
                    }
                };

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"LISTEN sse_broadcast; LISTEN \"sse_server_{_serverId}\"";
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                _logger.LogDebug("LISTEN connection established for server {ServerId}", _serverId);

                // Keep connection alive, processing notifications
                while (!ct.IsCancellationRequested)
                {
                    await conn.WaitAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LISTEN connection lost, reconnecting in 2s...");
                try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task HandleNotification(string channel, string payload)
    {
        // Check for overflow reference
        var actualPayload = payload;
        if (payload.Contains("\"r\""))
        {
            try
            {
                var refDoc = JsonDocument.Parse(payload);
                if (refDoc.RootElement.TryGetProperty("r", out var refProp))
                {
                    var msgId = Guid.Parse(refProp.GetString()!);
                    await using var conn = new NpgsqlConnection(_connectionString);
                    await conn.OpenAsync();

                    // Fetch and delete overflow message
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM sse_messages WHERE id = $1 RETURNING payload";
                    cmd.Parameters.AddWithValue(msgId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result is string overflowPayload)
                        actualPayload = overflowPayload;
                    else
                        return; // message already consumed
                }
            }
            catch (JsonException) { /* not a ref, use payload as-is */ }
        }

        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(actualPayload, DeserializeOptions);
        if (envelope == null) return;

        switch (envelope.Type)
        {
            case MessageType.Broadcast:
            {
                var evt = new SseEvent(null, envelope.Data);
                foreach (var state in _localConnections.Values)
                    await state.Channel.Writer.WriteAsync(evt);
                break;
            }
            case MessageType.Client:
            {
                if (envelope.TargetId != null && _localConnections.TryGetValue(envelope.TargetId, out var state))
                {
                    var evt = new SseEvent(envelope.GroupName, envelope.Data);
                    await state.Channel.Writer.WriteAsync(evt);
                }
                break;
            }
            case MessageType.Group:
            {
                if (envelope.TargetIds != null)
                {
                    var evt = new SseEvent(envelope.GroupName, envelope.Data);
                    foreach (var connectionId in envelope.TargetIds)
                    {
                        if (_localConnections.TryGetValue(connectionId, out var connState))
                            await connState.Channel.Writer.WriteAsync(evt);
                    }
                }
                break;
            }
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                // Refresh heartbeat for this server's connections
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE sse_connections SET last_heartbeat = now() WHERE server_id = $1";
                    cmd.Parameters.AddWithValue(_serverId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Clean up stale connections from crashed servers
                var staleThreshold = TimeSpan.FromSeconds(_connectionTtl.TotalSeconds * 2);
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"""
                        DELETE FROM sse_group_members WHERE connection_id IN (
                            SELECT connection_id FROM sse_connections
                            WHERE last_heartbeat < now() - interval '{staleThreshold.TotalSeconds} seconds'
                        );
                        DELETE FROM sse_connections
                        WHERE last_heartbeat < now() - interval '{staleThreshold.TotalSeconds} seconds';
                        """;
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Clean up expired overflow messages
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM sse_messages WHERE created_at < now() - interval '5 minutes'";
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                _logger.LogTrace("Heartbeat: refreshed {Count} connections", _localConnections.Count);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        try { _shutdownCts.Cancel(); } catch (ObjectDisposedException) { return; }

        try { await _listenTask; } catch (OperationCanceledException) { }
        try { await _heartbeatTask; } catch (OperationCanceledException) { }

        foreach (var connectionId in _localConnections.Keys.ToList())
            await DisconnectAsync(connectionId);

        // Clean up server's connections
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sse_connections WHERE server_id = $1";
        cmd.Parameters.AddWithValue(_serverId);
        await cmd.ExecuteNonQueryAsync();

        _shutdownCts.Dispose();
        _logger.LogInformation("Postgres backplane disposed. ServerId: {ServerId}", _serverId);
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

    private sealed class PostgresClients(PostgresBackplane backplane) : IBackplaneClients
    {
        public Task SendToAllAsync(object data) => backplane.SendToAllAsync(data);
        public Task SendToClientAsync(string connectionId, object data) => backplane.SendToClientAsync(connectionId, data);
        public async Task SendToClientsAsync(IEnumerable<string> connectionIds, object data)
        {
            var tasks = connectionIds.Select(id => backplane.SendToClientAsync(id, data));
            await Task.WhenAll(tasks);
        }
        public Task SendToGroupAsync(string groupName, object data) => backplane.SendToGroupAsync(groupName, data);
        public async Task SendToGroupsAsync(IEnumerable<string> groupNames, object data)
        {
            var tasks = groupNames.Select(g => backplane.SendToGroupAsync(g, data));
            await Task.WhenAll(tasks);
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
