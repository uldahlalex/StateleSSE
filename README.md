# StateleSSE

Realtime SSE framework for ASP.NET Core with live queries. Pair with the [`statele-sse`](statele-sse-client) npm package for a type-safe client.

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
dotnet add package StateleSSE.AspNetCore
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
        query: async groups => await groups.GetMembersAsync(roomGroup));

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

tood

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

### The RealtimeListenResponse

todo

## Live query system architecture visualization

todo

## Using without Entity Framework (simple backplane for basic event driven design & no live queries)

todo

## Scaling with Redis

Swap `AddInMemorySseBackplane()` for Redis to scale across multiple server instances:

```cs
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddRedisSseBackplane();
```

All backplane operations (send, groups, membership) work transparently across instances.
The EF.Realtime + Group Changes (like waiting for Entity Framework DbContext.SaveChanges()) does not currently support horizontal scaling.

## Examples

- [`ExampleApp.Quickstart`](ExampleApp.Quickstart) — minimal server + vanilla JS client
- [`ExampleApp.Chat`](ExampleApp.Chat) — full chat app with React, Redis, EfRealtime

## License

MIT
