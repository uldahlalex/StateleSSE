# StateleSSE

Type-safe, horizontally-scalable Server-Sent Events (SSE) framework for ASP.NET Core with a SignalR-style backplane.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## Quick Start

### 1. Server

```csharp
// Program.cs
using StateleSSE.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInMemorySseBackplane();  // or AddRedisSseBackplane() for scaling
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

```csharp
// RealtimeController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

public class RealtimeController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet("connect")]
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync();
        await using var connection = backplane.CreateConnection();

        await sse.WriteAsync("connected", JsonSerializer.Serialize(new { connection.ConnectionId }));

        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
            await sse.WriteAsync(evt.Group ?? "message", evt.Data);
    }

    [HttpPost("join")]
    public async Task Join(string connectionId, string room)
        => await backplane.Groups.AddToGroupAsync(connectionId, room);

    [HttpPost("send")]
    public async Task Send(string room, string message)
        => await backplane.Clients.SendToGroupAsync(room, new { message });
}
```

### 2. Client

```html
<div id="messages"></div>
<input id="msg" placeholder="Message" />
<button onclick="send()">Send</button>

<script>
const room = "chat";
let connectionId;

const es = new EventSource("/connect");
es.addEventListener("connected", e => {
    connectionId = JSON.parse(e.data).connectionId;
    fetch(`/join?connectionId=${connectionId}&room=${room}`, { method: "POST" });
});
es.addEventListener(room, e => {
    document.getElementById("messages").innerHTML += `<p>${JSON.parse(e.data).message}</p>`;
});

function send() {
    fetch(`/send?room=${room}&message=${document.getElementById("msg").value}`, { method: "POST" });
}
</script>
```

## Backplane API

### Clients (Publishing)

```csharp
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
```

### Groups (Membership)

```csharp
// Add/remove from group
await backplane.Groups.AddToGroupAsync(connectionId, "room-1");
await backplane.Groups.RemoveFromGroupAsync(connectionId, "room-1");

// Query membership
var members = await backplane.Groups.GetMembersAsync("room-1");
var count = await backplane.Groups.GetMemberCountAsync("room-1");
var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);
```

## Scaling with Redis

Switch to Redis for horizontal scaling:

```csharp
using StackExchange.Redis;

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse("localhost:6379");
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddRedisSseBackplane();
```

The Redis backplane handles cross-server routing, group membership, and connection cleanup automatically.

## OpenAPI String Constants

`StringConstantsDiscovery` helps expose event type names in your OpenAPI spec for client code generation. It extracts:
- All `BaseResponseDto` subclass names (event types)
- String constants from a class you specify

```csharp
using StateleSSE.AspNetCore;

// Get event type names
var eventTypes = StringConstantsDiscovery.GetEventTypeNames();

// Get constants from a specific class
var constants = StringConstantsDiscovery.GetStringConstants<MyConstants>();

// Get both combined
var all = StringConstantsDiscovery.GetAll<MyConstants>();
```

For NSwag integration, create a thin wrapper:

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
    config.AddStringConstants<MyConstants>();
});
```

## Example App

See `ExampleApp.Chat` for a complete implementation with React client.

## License

MIT
