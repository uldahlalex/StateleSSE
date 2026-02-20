using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using StateleSSE.AspNetCore.EfRealtime;
using System.Text.Json;

namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

internal sealed class PostgresBackplaneHostedService : IHostedService, IAsyncDisposable
{
    private readonly PostgresBackplane _backplane;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RealtimeManager? _realtimeManager;
    private readonly ILogger<PostgresBackplaneHostedService> _logger;
    private readonly TimeSpan _connectionTtl;
    private readonly CancellationTokenSource _cts = new();

    private Task _heartbeatTask = Task.CompletedTask;
    private Task _listenTask = Task.CompletedTask;

    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    public PostgresBackplaneHostedService(
        PostgresBackplane backplane,
        NpgsqlDataSource dataSource,
        IServiceScopeFactory scopeFactory,
        ILogger<PostgresBackplaneHostedService> logger,
        TimeSpan? connectionTtl = null,
        RealtimeManager? realtimeManager = null)
    {
        _backplane = backplane;
        _dataSource = dataSource;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _connectionTtl = connectionTtl ?? TimeSpan.FromSeconds(60);
        _realtimeManager = realtimeManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        _heartbeatTask = RunHeartbeatAsync(_cts.Token);
        _listenTask = RunListenAsync(_cts.Token);
        _logger.LogInformation("Postgres backplane started. ServerId: {ServerId}", _backplane.ServerId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
         _cts.Cancel();
        await Task.WhenAny(Task.WhenAll(_heartbeatTask, _listenTask), Task.Delay(5000, CancellationToken.None));
        _logger.LogInformation("Postgres backplane stopped");
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS "SseConnections" (
                "ConnectionId" TEXT PRIMARY KEY,
                "ServerId"     TEXT NOT NULL,
                "LastSeen"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS "SseConnectionGroups" (
                "ConnectionId" TEXT NOT NULL REFERENCES "SseConnections"("ConnectionId") ON DELETE CASCADE,
                "GroupName"    TEXT NOT NULL,
                PRIMARY KEY ("ConnectionId", "GroupName")
            );
            """);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_connectionTtl.TotalSeconds / 3);
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await _backplane.UpdateHeartbeatAsync(_backplane.GetLocalConnectionIds());
                await _backplane.DeleteStaleConnectionsAsync(_connectionTtl);
                _logger.LogTrace("Heartbeat: {Count} local connections", _backplane.GetLocalConnectionIds().Count());
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunListenAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            var npgsql = (NpgsqlConnection)conn;

            await using (var cmd = new NpgsqlCommand($"LISTEN \"sse:server:{_backplane.ServerId}\"", npgsql))
                await cmd.ExecuteNonQueryAsync(ct);
            await using (var cmd = new NpgsqlCommand("LISTEN \"sse:broadcast\"", npgsql))
                await cmd.ExecuteNonQueryAsync(ct);
            await using (var cmd = new NpgsqlCommand("LISTEN \"sse:ef:changes\"", npgsql))
                await cmd.ExecuteNonQueryAsync(ct);

            npgsql.Notification += (_, e) => _ = HandleNotificationAsync(e.Channel, e.Payload);

            while (!ct.IsCancellationRequested)
                await npgsql.WaitAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LISTEN loop error");
        }
    }

    private async Task HandleNotificationAsync(string channel, string payload)
    {
        try
        {
            if (channel == $"sse:server:{_backplane.ServerId}")
            {
                await _backplane.HandleServerMessageAsync(payload);
                return;
            }

            if (channel == "sse:broadcast")
            {
                await _backplane.HandleBroadcastMessageAsync(payload);
                return;
            }

            if (channel == "sse:ef:changes" && _realtimeManager is not null)
            {
                await HandleEfChangesAsync(payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling notification on channel {Channel}", channel);
        }
    }

    private async Task HandleEfChangesAsync(string payload)
    {
        var notifyChanges = JsonSerializer.Deserialize<NotifyChanges>(payload, DeserializeOptions);
        if (notifyChanges is null) return;

        var entries = new List<ChangeEntry>();
        foreach (var entry in notifyChanges.Entries)
        {
            var type = Type.GetType(entry.TypeAssemblyQualifiedName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
                    .FirstOrDefault(t => t.FullName == entry.TypeFullName);

            if (type is null) continue;

            object? instance = null;
            try { instance = Activator.CreateInstance(type); } catch { }
            if (instance is null) continue;

            entries.Add(new ChangeEntry(instance, (Microsoft.EntityFrameworkCore.EntityState)entry.State));
        }

        if (entries.Count == 0) return;

        var snapshot = new ChangeSnapshot(entries);
        var matched = _realtimeManager!.GetMatchingSubscriptions(snapshot);
        if (matched.Count == 0) return;

        var byContextType = matched.GroupBy(s => s.DbContextType);

        foreach (var group in byContextType)
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = (DbContext)scope.ServiceProvider.GetRequiredService(group.Key);

            foreach (var sub in group)
            {
                var data = await sub.Query(ctx);
                if (data is null) continue;
                await sub.Deliver(data);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Dispose();
        await Task.CompletedTask;
    }
}

internal sealed record NotifyEntry(string TypeAssemblyQualifiedName, string TypeFullName, int State);
internal sealed record NotifyChanges(NotifyEntry[] Entries);
