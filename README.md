# StateleSSE

Realtime SSE framework for ASP.NET Core. Each GET endpoint **is** the SSE stream — first event is the initial data snapshot, subsequent events are live updates triggered by EF `SaveChanges`.

No connectionId. No shared connection. No backplane. Mutations stay as plain HTTP.

## Installation

```
dotnet add package StateleSSE.AspNetCore
```

**Requirements:** .NET 6+, ASP.NET Core, Entity Framework Core.

## Setup

```csharp
// Program.cs
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<MyDbContext>((sp, conf) =>
{
    conf.UseNpgsql(connectionString);
    conf.AddEfRealtimeInterceptor(sp);
});
```

## How it works

Inherit from `RealtimeControllerBase` and call `ListenAsync` from any GET endpoint. The method:
1. Opens an SSE stream to the client
2. Sends the initial data as the first event
3. Re-runs `query` and streams a new event whenever `criteria` matches a `SaveChanges` on `TDbContext`
4. Closes the stream when the client disconnects

```csharp
public class ProductsController(IRealtimeManager realtimeManager, AppDbContext ctx)
    : RealtimeControllerBase(realtimeManager)
{
    [HttpGet(nameof(GetProducts))]
    [ProducesResponseType<List<Product>>(200)]
    public Task GetProducts(CancellationToken ct) =>
        ListenAsync<AppDbContext, List<Product>>(
            getInitialData: () => ctx.Products.ToListAsync(),
            criteria: changes => changes.HasChanges<Product>(),
            query: async c => await c.Products.ToListAsync(),
            ct);
}
```

The `[ProducesResponseType<T>(200)]` attribute is required for NSwag to generate correct TypeScript types for SSE endpoints (since the method returns `Task`, not `Task<T>`).

## Change criteria helpers

```csharp
// Any add/update/delete on Product
changes.HasChanges<Product>()

// Only additions
changes.HasAdded<Product>()

// Precise per-entity filtering
changes.OfType<Poke>().Any(e => e.Entity.ToUserId == userId)
```

## Targeted notifications via DB entity

The cleanest pattern for user-targeted events is to model them as DB entities and use `ListenAsync` with a precise `criteria`:

```csharp
// Entity
public class Poke
{
    public string Id { get; set; }
    public string FromUserId { get; set; }
    public string ToUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// Receiver stream — Authorize so we have a userId from JWT
[Authorize]
[HttpGet(nameof(GetPokes))]
[ProducesResponseType<List<Poke>>(200)]
public Task GetPokes(CancellationToken ct)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    return ListenAsync<AppDbContext, List<Poke>>(
        getInitialData: () => ctx.Pokes.Where(p => p.ToUserId == userId).ToListAsync(),
        criteria: changes => changes.OfType<Poke>().Any(e => e.Entity.ToUserId == userId),
        query: async c => await c.Pokes.Where(p => p.ToUserId == userId).ToListAsync(),
        ct);
}

// Sender mutation — plain HTTP
[Authorize]
[HttpPost(nameof(Poke))]
public async Task Poke(string toUserId)
{
    var fromUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    ctx.Pokes.Add(new Poke { Id = Guid.NewGuid().ToString(), FromUserId = fromUserId, ToUserId = toUserId, CreatedAt = DateTimeOffset.UtcNow });
    await ctx.SaveChangesAsync();
}
```

`GetPokes` only re-fires for the connected user's pokes. No connectionId lookup, no routing logic.

EventSource cannot set custom headers, so pass the JWT as a query param and configure JWT middleware to read it:

```csharp
o.Events = new JwtBearerEvents
{
    OnMessageReceived = ctx =>
    {
        var token = ctx.Request.Query["token"];
        if (!string.IsNullOrEmpty(token)) ctx.Token = token;
        return Task.CompletedTask;
    }
};
```

## Presence tracking

Use `SseConnection` / `SseConnectionGroup` as plain EF entities to track which users are online in a room. Manage their lifecycle in the endpoint:

```csharp
[HttpGet(nameof(GetMembers))]
[ProducesResponseType<List<MemberInfo>>(200)]
public async Task GetMembers(string roomId, CancellationToken ct)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var connectionId = Guid.NewGuid().ToString();
    var group = "members:" + roomId;

    ctx.SseConnections.Add(new SseConnection { ConnectionId = connectionId, ServerId = "local", LastSeen = DateTimeOffset.UtcNow, OwnerId = userId });
    ctx.SseConnectionGroups.Add(new SseConnectionGroup { ConnectionId = connectionId, GroupName = group });
    await ctx.SaveChangesAsync(ct);

    try
    {
        await ListenAsync<AppDbContext, List<MemberInfo>>(
            getInitialData: () => MembersQuery(ctx, roomId),
            criteria: changes => changes.HasChanges<SseConnectionGroup>(),
            query: async c => await MembersQuery(c, roomId),
            ct);
    }
    finally
    {
        ctx.SseConnectionGroups.Remove(new SseConnectionGroup { ConnectionId = connectionId, GroupName = group });
        ctx.SseConnections.Remove(new SseConnection { ConnectionId = connectionId, ServerId = "local", LastSeen = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync(CancellationToken.None);
    }
}
```

Define the entities in your own project (they are plain EF types, not part of the library):

```csharp
public class SseConnection
{
    public string ConnectionId { get; set; } = default!;
    public string ServerId { get; set; } = default!;
    public DateTimeOffset LastSeen { get; set; }
    public string? OwnerId { get; set; }
    public ICollection<SseConnectionGroup> Groups { get; set; } = [];
}

public class SseConnectionGroup
{
    public string ConnectionId { get; set; } = default!;
    public string GroupName { get; set; } = default!;
    public SseConnection Connection { get; set; } = default!;
}
```

Configure them in `OnModelCreating`:

```csharp
modelBuilder.Entity<SseConnection>(e => e.HasKey(x => x.ConnectionId));
modelBuilder.Entity<SseConnectionGroup>(e =>
{
    e.HasKey(x => new { x.ConnectionId, x.GroupName });
    e.HasOne(g => g.Connection)
     .WithMany(c => c.Groups)
     .HasForeignKey(g => g.ConnectionId)
     .OnDelete(DeleteBehavior.Cascade);
});
```

## TypeScript client — `makeSseStream`

NSwag generates typed methods like `getRooms(): Promise<Room[]>`. The trick below intercepts the URL from the generated client synchronously (before the actual fetch), then opens a real `EventSource` using that URL. TypeScript infers `T` from the return type.

```typescript
export function makeSseStream<T>(
    buildCall: (client: ChatClient) => Promise<T>,
    onData: (data: T) => void,
    token?: string
): EventSource {
    let url = '';
    const urlCapture = { fetch: (u: RequestInfo) => { url = u as string; return Promise.reject(); } };
    buildCall(new ChatClient(BASE_URL, urlCapture)).catch(() => {});
    if (token) url += (url.includes('?') ? '&' : '?') + `token=${encodeURIComponent(token)}`;
    const es = new EventSource(url);
    es.onmessage = e => onData(JSON.parse(e.data) as T);
    return es;
}
```

Usage in a React component:

```typescript
useEffect(() => {
    const token = localStorage.getItem('jwt') ?? undefined;

    const roomsEs    = makeSseStream(c => c.getRooms(), setRooms);
    const messagesEs = makeSseStream(c => c.getMessages(roomId), setMessages);
    const pokesEs    = token
        ? makeSseStream(c => c.getPokes(), setPokes, token)
        : null;

    return () => { roomsEs.close(); messagesEs.close(); pokesEs?.close(); };
}, [roomId]);
```

Each call opens one SSE stream. The stream stays open until the component unmounts. No manual reconnection — the browser handles retries via the `retry:` field.

## Guest / anonymous access

Issue tokens without requiring login:

```csharp
[HttpPost(nameof(GuestLogin))]
public LoginResponse GuestLogin() =>
    new LoginResponse(jwtService.GenerateToken(Guid.NewGuid().ToString()));
```

Guest tokens are valid JWTs. All `[Authorize]` endpoints work unchanged — the guest just has an ephemeral userId (a random GUID). Data they create is attributed to that GUID.

## Full example

See [`ExampleApp.Chat/`](ExampleApp.Chat/) for a working chat app using all features above: rooms, messages, presence, pokes, guest login, and TypeScript client generation.
