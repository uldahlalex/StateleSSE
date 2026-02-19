EF + Postgres LISTEN/NOTIFY SSE Backplane

Context

Currently the library offers InMemoryBackplane (single-server) and RedisBackplane (multi-server).
Goal: a third option using Postgres only — no Redis — for users who already run Postgres via EF.

Redis → Postgres equivalence (all push-based, no polling):

┌──────────────────────────┬───────────────────────────────────────────┐
│      Redis concept       │            Postgres equivalent            │
├──────────────────────────┼───────────────────────────────────────────┤
│ conn:{id} hash with TTL  │ SseConnections row with LastSeen          │
├──────────────────────────┼───────────────────────────────────────────┤
│ group:{name}:members set │ SseConnectionGroups join rows             │
├──────────────────────────┼───────────────────────────────────────────┤
│ PUBLISH / SUBSCRIBE      │ pg_notify() / LISTEN (Npgsql async wait)  │
├──────────────────────────┼───────────────────────────────────────────┤
│ Heartbeat refreshes TTL  │ Heartbeat UPDATE LastSeen every N seconds │
├──────────────────────────┼───────────────────────────────────────────┤
│ TTL expires → cleanup    │ Background DELETE WHERE LastSeen < cutoff │
└──────────────────────────┴───────────────────────────────────────────┘

LISTEN/NOTIFY is the key: Npgsql's NpgsqlConnection.WaitAsync(ct) blocks until a notification arrives — zero
polling, purely event-driven. This is architecturally identical to Redis SUBSCRIBE.

Files to create

Infrastructure/PostgresBackplane/

- SseConnection.cs — EF entity: ConnectionId string PK, ServerId string, LastSeen DateTimeOffset
- SseConnectionGroup.cs — EF entity: ConnectionId string, GroupName string (composite PK)
- SseBackplaneDbContext.cs — minimal DbContext with the 2 entities; also exposes static void
  ConfigureModel(ModelBuilder) for users merging into their own context
- PostgresBackplane.cs — implements ISseBackplane, mirrors RedisBackplane structure
- PostgresBackplaneHostedService.cs — IHostedService running heartbeat + cleanup + LISTEN loops

Extensions/

- PostgresServiceCollectionExtensions.cs — AddPostgresSseBackplane(connectionString) or
  AddPostgresSseBackplane<TDbContext>()

PostgresBackplane.cs key design

_localConnections  ConcurrentDictionary<string, ConnectionState>  (local Channel + Groups cache)
_db                IDbContextFactory<SseBackplaneDbContext>        (for metadata operations)
_serverId          Guid                                            (identifies this server instance)

Connect(): Insert SseConnection; add local Channel entry; return (reader, connectionId)

DisconnectAsync(): Delete SseConnectionGroups + SseConnection rows; complete Channel; fire events

AddToGroupAsync(): Upsert SseConnectionGroup row; update local dict cache

SendToGroupAsync():
1. Load member ConnectionIds + their ServerIds from SseConnectionGroups JOIN SseConnections
2. For local members → write to local Channel directly
3. For remote members → SELECT pg_notify('sse:{serverId}', '{json}') per target server

SendToClientAsync(): Check local dict → direct Channel write; else query DB for ServerId → pg_notify

SendToAllAsync(): Write to all local Channels + pg_notify('sse:broadcast', ...) once

PostgresBackplaneHostedService.cs (2 dedicated loops)

Heartbeat + Cleanup loop (PeriodicTimer every ttl/3):
UPDATE SseConnections SET LastSeen = NOW() WHERE ConnectionId IN (localIds)
DELETE FROM SseConnections WHERE LastSeen < NOW() - ttl
This is server-side maintenance (crash recovery), not client polling — equivalent to Redis TTL expiry.

LISTEN loop (truly push-based, no polling):
using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync(ct);
await conn.ExecuteAsync($"LISTEN \"sse:{_serverId}\"");
await conn.ExecuteAsync("LISTEN \"sse:broadcast\"");
conn.Notification += (_, e) => DeliverNotification(e.Payload);
while (!ct.IsCancellationRequested)
await conn.WaitAsync(ct);  // blocks until notification arrives, no polling

PostgresServiceCollectionExtensions.cs

// Standalone: library manages its own DbContext
services.AddPostgresSseBackplane("Host=...;Database=...;");

// Integrated: user's DbContext already has SSE tables configured
services.AddPostgresSseBackplane<MyAppDbContext>();
Registers PostgresBackplane as ISseBackplane (singleton) + PostgresBackplaneHostedService as IHostedService.

Critical files to mirror

- StateleSSE.AspNetCore/Infrastructure/RedisBackplane.cs — exact structural mirror
- StateleSSE.AspNetCore/Infrastructure/InMemoryBackplane.cs — ConnectionState inner class pattern
- StateleSSE.AspNetCore/ISseBackplane.cs — interface contract
- StateleSSE.AspNetCore/Extensions/RedisServiceCollectionExtensions.cs — DI registration pattern

Dependencies to add to .csproj

- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.EntityFrameworkCore (already present via EfRealtime)

Verification

1. Wire up AddPostgresSseBackplane(...) in ExampleApp.Chat pointing at a local Postgres instance
2. Run the app, connect 2 browser tabs, send a message — both should receive it
3. Close a tab → confirm DisconnectAsync removes DB row immediately
4. Wait for heartbeat cycle → confirm stale rows (simulated crash) are cleaned up
5. dotnet test StateleSSE.AspNetCore.IntegrationTests