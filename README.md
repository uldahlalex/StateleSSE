# StateleSSE

Type-safe, horizontally-scalable Server-Sent Events (SSE) framework for ASP.NET Core with a SignalR-style backplane.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
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

## Example app with scalability

Features Redis backplane, NSwag with codegen, horisontal scaling, increased typesafety + React client example

- [ExampleApp.Chat](ExampleApp.Chat)

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
    config.AddStringConstants<MyConstants>(); //MyConstants is arbitrary class name - will simply include string constants defined here
});
```

