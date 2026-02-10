# StateleSSE

Realtime SSE framework for ASP.NET Core with live queries. Pair with the [`statele-sse`](statele-sse-client) npm package for a type-safe client.

These docs are for the newest version (v4). For older version (before live queries with EF.Realtime), see branch "v3"

## Dependencies

| | Required     | Notes |
|---|-------------------------|---|
| .NET | 6.0+                    | Targets net6.0, net8.0, net9.0, net10.0 |
| ASP.NET Core | yes                     | `FrameworkReference` — comes with the SDK |
| Entity Framework Core | only for EfRealtime     | Bundled in the package but unused unless you call `AddEfRealtime()` |
| StackExchange.Redis | only for Redis backplane | Bundled in the package but unused unless you call `AddRedisSseBackplane()` |

Minimal setup (in-memory backplane, no EfRealtime) requires no additional packages from the consumer — just ASP.NET Core.

## Install

```bash
dotnet add package StateleSSE.AspNetCore --prerelease
```

## Quick start

> These snippets are from [`ExampleApp.Quickstart`](ExampleApp.Quickstart).

### Server

```cs
// ExampleApp.Quickstart/Program.cs

using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInMemorySseBackplane();
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<AppDb>((sp, opt) =>
{
    opt.UseInMemoryDatabase("quickstart");
    opt.AddEfRealtimeInterceptor(sp);
});
builder.Services.AddControllers();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Run();

```

```cs
// ExampleApp.Quickstart/AppDb.cs

using Microsoft.EntityFrameworkCore;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Message> Messages => Set<Message>();
}

public class Message
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

```

```cs
// ExampleApp.Quickstart/RealtimeController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

public class RealtimeController(ISseBackplane backplane, IRealtimeManager realtimeManager, AppDb db)
    : RealtimeControllerBase(backplane)
{
    /// <summary>
    ///Will produce the following in the browser's response tab:
    ///id: 2
    ///event: messages
    ///data: [{"id":1,"content":"hi","createdAt":"2026-02-09T10:34:37.1856196+00:00"},{"id":2,"content":"asd","createdAt":"2026-02-09T10:34:40.5670584+00:00"},{"id":3,"content":"a","createdAt":"2026-02-09T11:11:34.6666671+00:00"}]
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    [HttpGet("messages")]
    public async Task<RealtimeListenResponse<List<Message>>> GetMessages(string connectionId)
    {
        var group = "messages";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<AppDb>(connectionId, group,
            criteria: changes => changes.HasChanges<Message>(),
            query: async ctx => await ctx.Messages.OrderBy(m => m.CreatedAt).ToListAsync());

        return new RealtimeListenResponse<List<Message>>(group,
            await db.Messages.OrderBy(m => m.CreatedAt).ToListAsync());
    }

    /// <summary>
    /// Since this calls .SaveChangesAsync() on dbcontext, it triggers the "Listener" to make a new query and broadcast to the group
    /// </summary>
    /// <param name="message"></param>
    [HttpPost("send")]
    public async Task Send(string message)
    {
        db.Messages.Add(new Message { Content = message });
        await db.SaveChangesAsync();
    }
}

```

### Client

```html
<!-- ExampleApp.Quickstart/wwwroot/index.html -->

<!DOCTYPE html>
<html>
<body>
  <div id="messages"></div>
  <input id="msg" placeholder="Message" />
  <button onclick="send()">Send</button>

  <script type="module">
    import { StateleSSEClient } from 'https://cdn.jsdelivr.net/npm/statele-sse/dist/index.js'

    const sse = new StateleSSEClient("/sse");

    sse.listen(
        async (id) => {
          const res = await fetch(`/messages?connectionId=${id}`);
          return await res.json();
        },
        (data) => render(data)
    );

    function render(messages) {
      document.getElementById("messages").innerHTML =
        messages.map(m => `<p>${m.content}</p>`).join("");
    }

    window.send = () => {
      fetch(`/send?message=${document.getElementById("msg").value}`, { method: "POST" });
      document.getElementById("msg").value = "";
    };
  </script>
</body>
</html>

```

## EF Core Realtime

Automatic broadcasts when `SaveChanges` modifies data. Add a criteria (what change triggers it) and a query (what data to send).

### Setup for EF Core Realtime

```cs
builder.Services.AddInMemorySseBackplane();
builder.Services.AddEfRealtime();

builder.Services.AddDbContext<MyDbContext>((sp, options) => {
    options.UseNpgsql(connectionString);
    options.AddEfRealtimeInterceptor(sp);
});
```

### Subscribe endpoint for EF Core Realtime

```cs
public class ChatController(ISseBackplane backplane, IRealtimeManager realtime, MyDbContext ctx)
    : RealtimeControllerBase(backplane)
{
    [HttpGet(nameof(GetMessages))]
    public async Task<RealtimeListenResponse<List<Message>>> GetMessages(string connectionId, string roomId)
    {
        var group = $"room-messages:{roomId}";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtime.Subscribe<MyDbContext>(connectionId, group,
            criteria: changes => changes.OfType<Message>().Any(e => e.Entity.RoomId == roomId),
            query: async c => await c.Messages.Where(m => m.RoomId == roomId).ToListAsync());

        return new RealtimeListenResponse<List<Message>>(group, ctx.Messages.Where(m => m.RoomId == roomId).ToList());
    }
}
```

That's it. Any `SaveChanges` touching a `Message` with that `roomId` re-executes the query and broadcasts to all listeners.

## Group Realtime

Broadcasts driven by group membership changes (joins/leaves/disconnects) instead of DB changes.

### Setup

```cs
builder.Services.AddInMemorySseBackplane();
builder.Services.AddGroupRealtime();
```

### Example subscribe endpoint for getting all members in a group in realtime

```cs
[HttpGet(nameof(GetMembers))]
public async Task<RealtimeListenResponse<IReadOnlyList<string>>> GetMembers(string connectionId, string roomId)
{
    var listenGroup = $"room-members:{roomId}";
    var roomGroup = $"room-messages:{roomId}";
    await backplane.Groups.AddToGroupAsync(connectionId, listenGroup);

    groupRealtime.Subscribe(listenGroup,
        criteria: change => change.GroupName == roomGroup,
        query: async bp => await bp.Groups.GetMembersAsync(roomGroup));

    return new RealtimeListenResponse<IReadOnlyList<string>>(listenGroup,
        await backplane.Groups.GetMembersAsync(roomGroup));
}
```

## Client library: statele-sse

A very small TS/JS client can be downloaded from npm with:

```bash
npm i statele-sse
```


`listen` handles the full lifecycle — calls the endpoint, delivers initial data, then listens for SSE updates:

```ts
const url = '/sse'
const sse = new StateleSSEClient(url)

const unsub = sse.listen<Message[]>(
    (id) => fetch(`/GetMessages?connectionId=${id}&roomId=abc`).then(r => r.json()),
    (messages) => console.log(messages)
)
```
For more docs on the statele-sse-client, please see https://www.npmjs.com/package/statele-sse

## Public signatures & API reference

### "Criteria" for triggering a query with EF realtime:

```cs
//When using the realtimeManager:
        realtime.Subscribe<MyDbContext>(connectionId, group,
            criteria: changes => changes.OfType<Message>().Any(e => e.Entity.RoomId == roomId),
            query: async c => await c.Messages.Where(m => m.RoomId == roomId).ToListAsync()); 

//The criteria API is as following:
changes.OfType<Message>()
changes.HasChanges<Message>()
changes.HasAdded<Message>()
changes.HasModified<Message>()
changes.HasDeleted<Message>()
```

### "Criteria" for triggering a query with Group realtime manager:

```cs
//When using the groupRealtimeManager:
        groupRealtime.Subscribe(listenGroup,
            criteria: change => change.GroupName == roomGroup,
            query: async bp => await bp.Groups.GetMembersAsync(roomGroup));

//The criteria receives a GroupChangedEventArgs with:
change.ConnectionId  // the connection ID of the client whose membership changed
change.GroupName     // the group whose membership changed
change.ChangeType    // GroupChangeType.Added or GroupChangeType.Removed
```

### Backplane API

```cs
await backplane.Clients.SendToAllAsync(data);
await backplane.Clients.SendToGroupAsync("room-1", data);
await backplane.Clients.SendToGroupsAsync(["room-1", "room-2"], data);
await backplane.Clients.SendToClientAsync(connectionId, data);
await backplane.Clients.SendToClientsAsync([id1, id2], data);

await backplane.Groups.AddToGroupAsync(connectionId, "room-1");
await backplane.Groups.RemoveFromGroupAsync(connectionId, "room-1");
var members = await backplane.Groups.GetMembersAsync("room-1");
var count = await backplane.Groups.GetMemberCountAsync("room-1");
var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);
```

### The RealtimeListenResponse<T>

Since the initial HTTP response of a realtime query should return the "group" / event to listen for, the RealtimeListenResponse is used a long with optional initial data:

```cs
public record RealtimeListenResponse([Required][NotNull]string Group = null!);
public record RealtimeListenResponse<T>(
    [Required][NotNull] string Group = null!,
    T? Data = default
) : RealtimeListenResponse(Group);
```

The client library automatically is compliant with this "wrapper" object

```typescript
sse.listen<Room[]>(
    async (id) => await chatClient.getRooms(id), //this method technically returns RealtimeListenerResponse<Room[]>
    data => setRooms(data)  //here it is unwrapped when the state it used - this line will fire every time the "rooms" state is changed
);
```

## Live query system architecture visualization

```
 Browser                          ASP.NET Core Server
 ──────                          ──────────────────────────────────────────────────

  EventSource ───GET /sse──────► RealtimeControllerBase
       │                              │
       │◄── event: connected ─────────┘  (connectionId)
       │
       │                          Subscribe endpoint
       ├────GET /messages ──────► ┌──────────────────────────────────────────────┐
       │    ?connectionId=xxx     │ backplane.Groups.AddToGroup(connId, group)   │
       │                          │ realtimeManager.Subscribe(connId, group,     │
       │                          │     criteria: changes => ...,               │
       │                          │     query: async ctx => ...)                │
       │◄── { group, data } ──── │ return RealtimeListenResponse(group, data)   │
       │    (initial state)       └──────────────────────────────────────────────┘
       │
       │    sse.listen(onData)
       │    renders initial data
       │
       ·                          ┌──────────────────────────────────────────────┐
       ·                          │ Any mutation endpoint                        │
       ·    POST /send ─────────► │   db.Messages.Add(...)                      │
       ·                          │   db.SaveChangesAsync() ──┐                 │
       ·                          └───────────────────────────┼─────────────────┘
       ·                                                      ▼
       ·                          ┌──────────────────────────────────────────────┐
       ·                          │ SaveChangesInterceptor (automatic)           │
       ·                          │                                              │
       ·                          │  Before save:                                │
       ·                          │    snapshot = ChangeTracker entries           │
       ·                          │    matched = subscriptions.Where(criteria)   │
       ·                          │                                              │
       ·                          │  After save:                                 │
       ·                          │    for each matched subscription:            │
       ·                          │      result = await query(dbContext)          │
       ·                          │      backplane.SendToGroup(group, result) ───┼──┐
       ·                          └──────────────────────────────────────────────┘  │
       │                                                                            │
       │◄── event: messages ────────────── SSE push ◄──────────────────────────────┘
       │    data: [updated query result]
       │
       │    onData(newMessages)
       ▼    renders updated data
```

## Using without Entity Framework (simple backplane for basic event driven design & no live queries)

You can use the framework without Entity Framework at all. Simply remove the EF-related DI stuff:

```cs
builder.Services.AddInMemorySseBackplane();
//builder.Services.AddEfRealtime(); //not required

/*builder.Services.AddDbContext<MyDbContext>((sp, options) => {
    options.UseNpgsql(connectionString);
    options.AddEfRealtimeInterceptor(sp);
}); not required either */
```

And then rely on the backplane for doing the client management / broadcasting:

```cs
public class ChatController(ISseBackplane backplane) : RealtimeControllerBase(backplane)
{
    public async Task AddToGroup(string connectionId)
    {
        await backplane.Groups.AddToGroupAsync(connectionId, "room1");
    }

    public async Task SendToGrpoup(string message)
    {
        await backplane.Clients.SendToGroupAsync("room1", message);
    }
}
```

## Scaling with Redis

Swap `AddInMemorySseBackplane()` for Redis to scale across multiple server instances:

```cs
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddRedisSseBackplane();
```

All backplane operations (send, groups, membership) work transparently across instances.
The EF.Realtime + Group Changes (like waiting for Entity Framework DbContext.SaveChanges()) does not currently support horizontal scaling.

## JsonSerializer settings

To change broadcast serialization behavior, simply change the DI for controller's native serializer:

```cs
//example with arbitrary JSON serializer settings
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
```

## Examples

- [`ExampleApp.Quickstart`](ExampleApp.Quickstart) — minimal server + vanilla JS client
- [`ExampleApp.Chat`](ExampleApp.Chat) — full chat app with React, Redis, EfRealtime

## License

MIT
