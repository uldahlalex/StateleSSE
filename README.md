# StateleSSE

A type-safe, horizontally-scalable Server-Sent Events (SSE) framework for ASP.NET Core.

## What is StateleSSE?

StateleSSE provides a clean, production-ready abstraction for building real-time Server-Sent Events APIs in .NET. It eliminates SSE boilerplate and enables horizontal scaling through a backplane architecture.

Key features:
- Type-safe event streaming with minimal boilerplate
- Horizontal scaling via backplane abstraction (Redis or in-memory)
- Channel-based pub/sub with flexible naming conventions
- TypeScript client code generation
- OpenAPI/Swagger integration

## Installation

### Basic Setup (Single Server)

```bash
dotnet add package StateleSSE.AspNetCore
```

### Production Setup (Multi-Server with Redis)

```bash
dotnet add package StateleSSE.AspNetCore
dotnet add package StateleSSE.Backplane.Redis
```

## Quick Start

### 1. Register the backplane

**Development (in-memory):**
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInMemorySseBackplane();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

**Production (Redis):**
```csharp
using StateleSSE.Backplane.Redis.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRedisSseBackplane(options =>
{
    options.RedisConnectionString = "localhost:6379";
    options.ChannelPrefix = "myapp";
});
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

### 2. Define your event types

```csharp
public record MessageReceivedEvent(string Username, string Content, DateTime Timestamp);
public record UserJoinedEvent(string Username);
```

### 3. Create an SSE endpoint

**Basic approach (AspNetCore package):**
```csharp
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

[ApiController]
[Route("api/chat")]
public class ChatController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet("subscribe/{roomId}")]
    [EventSourceEndpoint(typeof(MessageReceivedEvent))]
    public Task SubscribeToRoom(string roomId)
    {
        var channel = $"chat:{roomId}";
        return HttpContext.StreamSseAsync(backplane, channel);
    }
}
```

**Production approach (Redis backplane with SseControllerBase):**
```csharp
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;
using StateleSSE.Backplane.Redis;

[ApiController]
public class ChatController(ISseBackplane backplane) : SseControllerBase(backplane)
{
    [HttpGet("subscribe/{roomId}")]
    [EventSourceEndpoint(typeof(MessageReceivedEvent))]
    public async Task SubscribeToRoom(string roomId)
    {
        var channel = $"chat:{roomId}:MessageReceivedEvent";
        await StreamEventType<MessageReceivedEvent>(channel);
    }
}
```

The `SseControllerBase` adds production features:
- Automatic keepalives (prevents ANCM timeout in IIS/Azure)
- Event IDs for reconnection tracking
- Retry directive for automatic reconnection
- nginx buffering prevention

### 4. Publishing events

```csharp
[HttpPost("rooms/{roomId}/messages")]
public async Task<IActionResult> SendMessage(
    string roomId,
    [FromBody] SendMessageRequest request)
{
    var evt = new MessageReceivedEvent(
        request.Username,
        request.Content,
        DateTime.UtcNow
    );

    var channel = $"chat:{roomId}:MessageReceivedEvent";
    await backplane.PublishToGroup(channel, evt);

    return Ok();
}

public record SendMessageRequest(string Username, string Content);
```

### 5. Consume from a client

```javascript
const eventSource = new EventSource('/api/chat/subscribe/room-123');

eventSource.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log(`${data.Username}: ${data.Content}`);
};

eventSource.onerror = (error) => {
    console.error('SSE error:', error);
    eventSource.close();
};
```

## Advanced Usage

### Streaming with Initial State

Send current state immediately when a client connects (basic approach):

```csharp
[HttpGet("subscribe/{gameId}")]
public async Task SubscribeToGame(string gameId)
{
    var channel = $"game:{gameId}";

    return await HttpContext.StreamSseWithInitialStateAsync(
        backplane,
        channel,
        getInitialState: async () => await GetGameState(gameId)
    );
}
```

### Channel Naming Convention

Use string interpolation for channel names. Common patterns:

```csharp
// Domain-scoped with event type (recommended for production)
var channel = $"game:{gameId}:PlayerJoinedEvent";

// Simple domain-scoped
var channel = $"chat:{roomId}";

// Broadcast channel
var channel = "notifications:all";
```

Optional: `ChannelNamingExtensions` provides helper methods if you prefer:
```csharp
var channel = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", gameId);
```

### Publishing to Multiple Channels

```csharp
// Publish to multiple rooms at once
var channels = new[] { "chat:room-1", "chat:room-2", "chat:room-3" };
await backplane.PublishToGroups(channels, evt);

// Broadcast to all connected clients
await backplane.PublishToAll(new ServerMaintenanceEvent());
```

### Backplane Diagnostics

```csharp
var diagnostics = backplane.GetDiagnostics();
Console.WriteLine($"Active groups: {diagnostics.TotalGroups}");
Console.WriteLine($"Total subscribers: {diagnostics.TotalLocalSubscribers}");

foreach (var group in diagnostics.Groups)
{
    Console.WriteLine($"{group.GroupId}: {group.LocalSubscribers} subscribers");
}
```

## Architecture

### Backplane Pattern

The `ISseBackplane` abstraction enables horizontal scaling:

1. Clients connect to any server instance via SSE endpoints
2. Each server subscribes to channels via the backplane
3. When you publish an event, it's distributed across all server instances via the backplane
4. Each server delivers events to its local SSE connections

This allows you to scale to multiple servers while maintaining real-time event delivery.

### Available Backplanes

- **InMemoryBackplane**: Single-server deployments, development, testing
- **RedisBackplane**: Production multi-server deployments using Redis pub/sub

## TypeScript Code Generation

StateleSSE can automatically generate type-safe TypeScript EventSource clients from your SSE endpoints.

**StateleSSE is completely OpenAPI-agnostic**. It supports all three major OpenAPI libraries without requiring any of them:
- NSwag
- Swashbuckle
- Microsoft.AspNetCore.OpenApi (.NET 9+)

### Setup

**Step 1: Add your preferred OpenAPI library**

Choose one (or none if you don't need TypeScript codegen):

```bash
# NSwag
dotnet add package NSwag.AspNetCore

# OR Swashbuckle
dotnet add package Swashbuckle.AspNetCore

# OR Microsoft.AspNetCore.OpenApi (.NET 9+)
dotnet add package Microsoft.AspNetCore.OpenApi
```

**Step 2: Configure OpenAPI and add the StateleSSE processor/filter**

StateleSSE automatically detects which OpenAPI library you're using and provides the appropriate integration:

**NSwag:**
```csharp
using StateleSSE.AspNetCore.CodeGen;

services.AddOpenApiDocument(conf =>
{
    conf.OperationProcessors.Add(new NSwagEventSourceProcessor());
});
```

**Swashbuckle:**
```csharp
using StateleSSE.AspNetCore.CodeGen;

services.AddSwaggerGen(c =>
{
    c.OperationFilter<SwashbuckleEventSourceFilter>();
});
```

**Microsoft.AspNetCore.OpenApi (.NET 9+):**
```csharp
using StateleSSE.AspNetCore.CodeGen;

builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer<MicrosoftOpenApiEventSourceTransformer>();
});
```

**Step 3: Mark your SSE endpoints**

```csharp
[HttpGet(nameof(StreamMessages))]
[EventSourceEndpoint(typeof(MessageReceivedEvent))]
public async Task StreamMessages(string roomId)
{
    var channel = $"chat:{roomId}:MessageReceivedEvent";
    await StreamEventType<MessageReceivedEvent>(channel);
}
```

**Step 4: Generate TypeScript client at startup**

```csharp
var app = builder.Build();

app.UseOpenApi(); // or app.UseSwagger() for Swashbuckle

// Generate TypeScript EventSource client
TypeScriptSseGenerator.Generate(
    openApiSpecPath: "openapi-with-docs.json",
    outputPath: "../client/src/generated-sse-client.ts"
);

await app.RunAsync();
```

### Generated TypeScript

The generator creates type-safe EventSource functions:

```typescript
/**
 * Subscribe to MessageReceivedEvent events
 * @param roomid - RoomId
 * @returns EventSource instance for MessageReceivedEvent
 */
export function streamMessages(roomid: string): EventSource {
    const queryParams = new URLSearchParams({ roomid });
    const url = `${BASE_URL}/StreamMessages?${queryParams}`;
    return new EventSource(url);
}
```

### Usage in your frontend

```typescript
import { streamMessages } from './generated-sse-client';

const es = streamMessages('room-123');

es.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log(data.Username, data.Content);
};

es.onerror = () => {
    console.error('Connection lost');
    es.close();
};
```

## License

MIT
