# StateleSSE

Type-safe, horizontally-scalable Server-Sent Events (SSE) framework for ASP.NET Core with a SignalR-style backplane.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## Depenendency Injection

When doing group / client management for broadcasting / pushing data, the ISseBackplane abstraction can be used. It must be injected as following:

```csharp
builder.Services.AddRedisSseBackplane(configure: conf =>
{
    conf.RedisConnectionString = "localhost:6379";
});
```
Or if you want separate IConnectionMultiplexer for Redis (the "traditional" DI for Redis), you can do it in separate statements:
```csharp
//With redis backplane:
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var config = ConfigurationOptions.Parse(
            "localhost:6379"
            );
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config);
    });

builder.Services.AddRedisSseBackplane();
````
For simple testing / non horizontal scaling, the simple InMemoryBackplane can be used instead:

```csharp
builder.Services.AddInMemorySseBackplane();
```


### Set up a route for connecting to the API + adding connection to backplane (for client/group management):

```csharp

//You may also use Minimal API / other framework, as long as you can access the HttpContext.
public class ChatController(ISseBackplane backplane) : ControllerBase
{
    /* this will produce the following response:
       id: 1
       event: ConnectionResponse
       data: {"connectionId":"8cc4cabc-e550-4e20-9732-5da6282f573b","eventType":"ConnectionResponse"}
     */
    [HttpGet(nameof(Connect))]
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync(); 
        await using var connection = backplane.CreateConnection();

        await sse.WriteAsync("ConnectionResponse", 
            JsonSerializer.Serialize(new {eventType = "ConnectionResponse", connectionId = connection.ConnectionId});
        
        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }

```

You may simply connect to the API using a simple "EventSource" from the browser like:

```js
const es = new EventSource('http://localhost:5000/connect')
es.addEventListener('group', e => {
    const data = JSON.parse(e.data);
    //do something with the data
})
```

(Also see "React client best practices" further down if you're using React)


### Using the backplane methods

```csharp
using StateleSSE.AspNetCore;

public class Example(ISseBackplane backplane)
{
    public async Task Send(string connectionId, object data, string id1 = "abc", string id2 = "xyz")
    {
        // Broadcast to all
        await backplane.Clients.SendToAllAsync(data);

        // Send to group
        await backplane.Clients.SendToGroupAsync("room-1", data);

        // Send to multiple groups
        await backplane.Clients.SendToGroupsAsync(["room-1", "room-2"], data);

        // Send to specific client
        await backplane.Clients.SendToClientAsync(connectionId, data);

        // Send to multiple clients
        await backplane.Clients.SendToClientsAsync([id1, id2], data);
    }

    public async Task Group(string connectionId)
    {
        // Add/remove from group
        await backplane.Groups.AddToGroupAsync(connectionId, "room-1");
        await backplane.Groups.RemoveFromGroupAsync(connectionId, "room-1");

        // Query membership
        var members = await backplane.Groups.GetMembersAsync("room-1");
        var count = await backplane.Groups.GetMemberCountAsync("room-1");
        var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);
    }
}

```

### React best practices

I recommend using the useStream hook I have placed in [useStream.tsx](ExampleApp.Chat/client/src/useStream.tsx)

Simply copy the file contents of useStream.tsx into your project and add the provider like this:

```jsx
<StreamProvider config={{
    urlForStreamEndpoint: `${BASE_URL}/Connect`, //Simply target the endpoint for connecting with a GET request
    connectEvent: "ConnectionResponse", //This value must correspond with the event string emitted by the connection method
}}>
    <Chat/>
</StreamProvider>
```

And use the useStream hook in a React component like this:

```tsx
export function Group(params: GroupParams) {
    const stream = useStream(); //exposes the on<T>(group, eventType, action) method + the connectionId for the current client
    useEffect(() => {
        stream.on<JoinGroupResponse>(
            "room/123", //always place the "group", so the same "key" as when using the backplane on the server
            "JoinGroupResponse", //the value for the "eventType" property in the sent object to the client
            (dto) => {
                alert('Someone has joined the room')
            });
    })
}
```

There is also a video demonstration here: https://www.youtube.com/watch?v=uTZ4b-X64nU

### C# BaseResponseDto

Since the useStream assumes the server always attached "eventType" in the JSON response (camelCase), you can extend BaseResponseDto for return types to automatically include this:


```csharp
public record MessageResponseDto : BaseResponseDto
{
    public string Message { get; set; } = "";
    public string? User { get; set; } = "";
}
```


## Quick Start

See [`ExampleApp.Quickstart`](ExampleApp.Quickstart) for a minimal working example:

- [Program.cs](ExampleApp.Quickstart/Program.cs) - Server setup
- [RealtimeController.cs](ExampleApp.Quickstart/RealtimeController.cs) - SSE endpoints
- [wwwroot/index.html](ExampleApp.Quickstart/wwwroot/index.html) - Browser client

Run it:
```bash
cd ExampleApp.Quickstart
dotnet run
#then navigate to http://localhost:5000 to use the client app
```

The quickstart is also demo'ed on my Youtube channel here: https://www.youtube.com/watch?v=2TI6JUEHw4k

## Example app with scalability and more "best practices"

Features Redis backplane, NSwag with codegen, horisontal scaling, increased typesafety + React client example

- [ExampleApp.Chat](ExampleApp.Chat)


## EF Core Realtime (EfRealtime)

Automatic realtime updates for web clients driven by EF Core's `SaveChanges`. When data is saved, registered queries are executed and results are broadcast to subscribers via the SSE backplane.

### How it works

1. A controller endpoint **subscribes** clients to a realtime feature by defining a **criteria** (what kind of change triggers it) and a **query** (what data to send).
2. Clients join a backplane group for that feature.
3. When `SaveChanges`/`SaveChangesAsync` is called on the DbContext, the interceptor:
   - Snapshots the change tracker **before** save (so Added/Modified/Deleted states are visible)
   - After save succeeds, executes the query against the committed database state
   - Broadcasts the result to the backplane group

### Setup

```csharp
builder.Services.AddInMemorySseBackplane(); // or AddRedisSseBackplane()
builder.Services.AddEfRealtime();

builder.Services.AddDbContext<MyDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddEfRealtimeInterceptor(sp); // hooks into SaveChanges
});
```

### Subscribing clients to realtime features

In a controller, use `IRealtimeManager` to register a subscription. The criteria inspects a `ChangeSnapshot` captured before save, and the query runs after save:

```csharp
public class ChatController(ISseBackplane backplane, IRealtimeManager realtime) : ControllerBase
{
    [HttpPost("listen/room-messages/{roomId}")]
    public async Task<IActionResult> ListenToRoomMessages(string connectionId, int roomId)
    {
        var group = $"room-messages:{roomId}";

        // Add client to the backplane group
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        // Register the realtime subscription for this group
        realtime.Subscribe<MyDbContext>(group,
            criteria: changes => changes.OfType<Message>()
                .Any(e => e.State == EntityState.Added && e.Entity.RoomId == roomId),
            query: async ctx => await ctx.Messages
                .Where(m => m.RoomId == roomId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .ToListAsync()
        );

        return Ok();
    }
}
```

Now whenever *any* code calls `SaveChangesAsync()` on `MyDbContext` and a `Message` with matching `RoomId` was added, the query executes and all clients in the `"room-messages:{roomId}"` group receive the result.

### ChangeSnapshot API

The `criteria` function receives a `ChangeSnapshot` with these helpers:

```csharp
// Typed access to changed entities
changes.OfType<Message>()          // IEnumerable<ChangeEntry<Message>>

// Quick checks
changes.HasChanges<Message>()      // any Added, Modified, or Deleted
changes.HasAdded<Message>()        // any Added
changes.HasModified<Message>()     // any Modified
changes.HasDeleted<Message>()      // any Deleted

// Each ChangeEntry<T> has:
//   .Entity  (T)           - the entity instance
//   .State   (EntityState)  - Added, Modified, or Deleted
```

### Unsubscribing

```csharp
realtime.Unsubscribe("room-messages:5");
```

Calling `Subscribe` with the same group name replaces the previous subscription (no duplicates).

---

## OpenAPI String Constants

`StringConstantsDiscovery` helps expose event type names in your OpenAPI spec for client code generation. It extracts:
- All `BaseResponseDto` subclass names (event types)
- String constants from a class you specify

This way you don't have to use error prone hardcoded string in your client app. This allows for:

```tsx
const stream = useStream();
useEffect(() => {
    stream.on<JoinGroupResponse>(params.room.id!,
        StringConstants.JoinGroupResponse, //this comes from OpenAPI-based scaffolded TS code. Also see the ExampleApp.Chat/ which uses NSwag to do this
        (dto) => {
        setMembers(dto.members ?? []);
    });
}, [])

```



For NSwag integration, create a thin wrapper: (assuming you have already added required NSwag Nugets)

```csharp
using NJsonSchema;
using NSwag.Generation;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using StateleSSE.AspNetCore;

public static class NSwagExtensions
{
    public static void AddStringConstants<T>(this OpenApiDocumentGeneratorSettings settings)
    {
        settings.DocumentProcessors.Add(new StringConstantsProcessor<T>());
    }

    private sealed class StringConstantsProcessor<T> : IDocumentProcessor
    {
        public void Process(DocumentProcessorContext context)
        {
            var schema = new JsonSchema { Type = JsonObjectType.String };
            foreach (var c in StringConstantsDiscovery.GetAll<T>())
                schema.Enumeration.Add(c);
            context.Document.Definitions["StringConstants"] = schema;
        }
    }
}
```

Then in `Program.cs`:

```csharp
builder.Services.AddOpenApiDocument(config =>
{
    config.AddStringConstants<MyConstants>(); //MyConstants is arbitrary class name - will simply include string constants defined here
});
```

