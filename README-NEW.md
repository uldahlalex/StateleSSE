# StateleSSE.AspNetCore — Quickstart

Server-Sent Events with group/client targeting and optional horizontal scaling.

## Install

```
dotnet add package StateleSSE.AspNetCore
```

---

## Single server (in-memory)

**Program.cs**
```csharp
using StateleSSE.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInMemorySseBackplane();
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<AppDb>((sp, opt) =>
{
    opt.UseNpgsql(connectionString);
    opt.AddEfRealtimeInterceptor(sp);
});
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

---

## Multi-server (Postgres NOTIFY/LISTEN)

**Program.cs**
```csharp
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPostgresSseBackplane("Host=localhost;Database=mydb;Username=postgres;Password=secret");
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<AppDb>((sp, opt) =>
{
    opt.UseNpgsql(connectionString);
    opt.AddPostgresEfRealtimeInterceptor(sp); // sends pg_notify after SaveChanges
});
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

> Use a **direct** (non-pooler) Postgres connection string for the backplane — PgBouncer and other poolers do not support `LISTEN`/`NOTIFY`.

---

## Controller

```csharp
public class MessagesController(ISseBackplane backplane, IRealtimeManager realtimeManager, AppDb db)
    : RealtimeControllerBase(backplane)
{
    // 1. Client opens SSE stream — returns an event stream, keep alive until disconnect
    [HttpGet("stream")]
    public IActionResult Stream(string connectionId) => Connect();

    // 2. Client subscribes to live message updates for a room
    //    Returns current data immediately + the group name to listen on
    [HttpGet("messages")]
    public async Task<RealtimeListenResponse<List<Message>>> GetMessages(string connectionId, string roomId)
    {
        await realtimeManager.SubscribeAsync<AppDb>(
            connectionId,
            groupName: $"messages:{roomId}",
            criteria: changes => changes.HasChanges<Message>(),
            query: async ctx => await ctx.Messages
                .Where(m => m.RoomId == roomId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync());

        return new RealtimeListenResponse<List<Message>>(
            $"messages:{roomId}",
            await db.Messages.Where(m => m.RoomId == roomId).ToListAsync());
    }

    // 3. Any SaveChangesAsync() call triggers criteria → query → broadcast to the group
    [HttpPost("messages")]
    public async Task PostMessage(string roomId, string content)
    {
        db.Messages.Add(new Message { RoomId = roomId, Content = content });
        await db.SaveChangesAsync();
    }
}
```

### How it fits together

```
Client A                        Server                         Postgres
  │                               │                               │
  ├── GET /stream?connectionId=x ─►│ Connect() → SSE stream open  │
  │                               │                               │
  ├── GET /messages?connectionId=x►│ SubscribeAsync()              │
  │   ◄─ { group, currentData } ──│   AddToGroupAsync (DB row)    │
  │                               │   register criteria+query     │
  │                               │                               │
Client B                          │                               │
  ├── POST /messages ────────────►│ SaveChangesAsync()            │
  │                               │   ├── INSERT committed ──────►│
  │                               │   └── pg_notify('sse:ef:…') ─►│
  │                               │                               │
  │                               │◄── NOTIFY received ───────────┤
  │                               │   criteria matched            │
  │                               │   query executed              │
  ◄── SSE event: updated list ────│   pushed to local connections │
```

---

## Targeting specific clients or groups directly

```csharp
// Send to one client
await backplane.Clients.SendToClientAsync(connectionId, new { text = "hello" });

// Send to everyone in a group
await backplane.Clients.SendToGroupAsync("messages:room-1", new { text = "hello" });

// Broadcast to all connected clients across all servers
await backplane.Clients.SendToAllAsync(new { text = "hello" });
```

---

## Unsubscribe

```csharp
// Manually remove from one group
await realtimeManager.UnsubscribeAsync(connectionId, "messages:room-1");

// All subscriptions cleaned up automatically on disconnect
```
