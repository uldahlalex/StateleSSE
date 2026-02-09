# StateleSSE

Realtime SSE framework for ASP.NET Core with horizontal scaling via Redis. Pair with the [`statele-sse`](statele-sse-client) npm package for a type-safe client.

## Install

```bash
dotnet add package StateleSSE.AspNetCore
```

## Quick start

### Server

```csharp
// Program.cs
builder.Services.AddInMemorySseBackplane(); // or AddRedisSseBackplane() for scaling
builder.Services.AddControllers();
```

```csharp
public class MyController(ISseBackplane backplane) : RealtimeControllerBase(backplane)
{
    // RealtimeControllerBase provides GET /sse automatically

    [HttpPost("join")]
    public async Task Join(string connectionId, string room)
        => await backplane.Groups.AddToGroupAsync(connectionId, room);

    [HttpPost("send")]
    public async Task Send(string room, string message)
        => await backplane.Clients.SendToGroupAsync(room, new { message });
}
```

### Client

```bash
npm i statele-sse
```

```ts
import { StateleSSEClient } from 'statele-sse'

const sse = new StateleSSEClient('http://localhost:5000/sse')

sse.onStatusChange = (status) => console.log(status)

const unsub = sse.listen(
    async (id) => {
        await fetch(`/join?connectionId=${id}&room=chat`, { method: 'POST' })
        return { group: 'chat' }
    },
    (data) => console.log(data)
)
```

## EF Core Realtime

Automatic broadcasts when `SaveChanges` modifies data. Add a criteria (what change triggers it) and a query (what data to send).

### Setup

```csharp
builder.Services.AddInMemorySseBackplane();
builder.Services.AddEfRealtime();

builder.Services.AddDbContext<MyDbContext>((sp, options) => {
    options.UseNpgsql(connectionString);
    options.AddEfRealtimeInterceptor(sp);
});
```

### Subscribe endpoint

```csharp
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

### Client

`listen` handles the full lifecycle — calls the endpoint, delivers initial data, then listens for SSE updates:

```ts
const sse = new StateleSSEClient('http://localhost:5000/sse')

const unsub = sse.listen<Message[]>(
    (id) => fetch(`/GetMessages?connectionId=${id}&roomId=abc`).then(r => r.json()),
    (messages) => console.log(messages)
)
```

Any `SaveChanges` touching a `Message` with that `roomId` re-executes the query and broadcasts to all listeners.

### ChangeSnapshot API

```csharp
changes.OfType<Message>()       // IEnumerable<ChangeEntry<Message>>
changes.HasChanges<Message>()   // any Added, Modified, or Deleted
changes.HasAdded<Message>()
changes.HasModified<Message>()
changes.HasDeleted<Message>()
// Each ChangeEntry<T> has .Entity (T) and .State (EntityState)
```

## Group Realtime

Broadcasts driven by group membership changes (joins/leaves/disconnects) instead of DB changes.

### Setup

```csharp
builder.Services.AddInMemorySseBackplane();
builder.Services.AddGroupRealtime();
```

### Subscribe endpoint

```csharp
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

## Backplane API

```csharp
// Send
await backplane.Clients.SendToAllAsync(data);
await backplane.Clients.SendToGroupAsync("room-1", data);
await backplane.Clients.SendToGroupsAsync(["room-1", "room-2"], data);
await backplane.Clients.SendToClientAsync(connectionId, data);
await backplane.Clients.SendToClientsAsync([id1, id2], data);

// Groups
await backplane.Groups.AddToGroupAsync(connectionId, "room-1");
await backplane.Groups.RemoveFromGroupAsync(connectionId, "room-1");
var members = await backplane.Groups.GetMembersAsync("room-1");
var count = await backplane.Groups.GetMemberCountAsync("room-1");
var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);
```

## Scaling with Redis

Swap `AddInMemorySseBackplane()` for Redis to scale across multiple server instances:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddRedisSseBackplane();
```

All backplane operations (send, groups, membership) work transparently across instances.

## Examples

- [`ExampleApp.Quickstart`](ExampleApp.Quickstart) — minimal server + vanilla JS client
- [`ExampleApp.Chat`](ExampleApp.Chat) — full chat app with React, Redis, EfRealtime, and horizontal scaling

## License

MIT
