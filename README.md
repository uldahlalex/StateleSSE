# StateleSSE

Type-safe, horizontally-scalable Server-Sent Events (SSE) framework for ASP.NET Core with a SignalR-style backplane.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## Quick Start

### 1. Register the backplane

```csharp
using StateleSSE.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Option A: In-memory (development / single server)
builder.Services.AddInMemorySseBackplane();

// Option B: Redis (production / horizontal scaling)
// builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
//     ConnectionMultiplexer.Connect("localhost:6379"));
// builder.Services.AddRedisSseBackplane();

builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

### 2. Create the realtime controller

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;  // Required for OpenSseStreamAsync, CreateConnection extensions

[ApiController]
[Route("api/realtime")]
public class RealtimeController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// Open SSE connection. Returns connectionId immediately.
    /// </summary>
    [HttpGet("connect")]
    [Produces("text/event-stream")]
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync();
        await using var connection = backplane.CreateConnection();

        // Send connectionId to client immediately
        await sse.WriteAsync("connected", JsonSerializer.Serialize(new
        {
            connectionId = connection.ConnectionId
        }));

        // Stream events to client
        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }

    /// <summary>
    /// Join a group.
    /// </summary>
    [HttpPost("groups/join")]
    public async Task<IActionResult> JoinGroup([FromBody] JoinGroupRequest request)
    {
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group);
        var members = await backplane.Groups.GetMembersAsync(request.Group);

        // Notify group of new member
        await backplane.Clients.GroupAsync(request.Group, new
        {
            type = "join",
            connectionId = request.ConnectionId,
            memberCount = members.Count
        });

        return Ok(new { memberCount = members.Count });
    }

    /// <summary>
    /// Leave a group.
    /// </summary>
    [HttpPost("groups/leave")]
    public async Task<IActionResult> LeaveGroup([FromBody] LeaveGroupRequest request)
    {
        await backplane.Groups.RemoveFromGroupAsync(request.ConnectionId, request.Group);
        var members = await backplane.Groups.GetMembersAsync(request.Group);

        await backplane.Clients.GroupAsync(request.Group, new
        {
            type = "leave",
            connectionId = request.ConnectionId,
            memberCount = members.Count
        });

        return Ok(new { memberCount = members.Count });
    }

    /// <summary>
    /// Send message to a group.
    /// </summary>
    [HttpPost("groups/send")]
    public async Task<IActionResult> SendToGroup([FromBody] SendToGroupRequest request)
    {
        await backplane.Clients.GroupAsync(request.Group, new
        {
            from = request.ConnectionId,
            payload = request.Payload
        });
        return Ok();
    }

    /// <summary>
    /// Send message to a specific client.
    /// </summary>
    [HttpPost("clients/send")]
    public async Task<IActionResult> SendToClient([FromBody] SendToClientRequest request)
    {
        await backplane.Clients.ClientAsync(request.TargetConnectionId, new
        {
            from = request.FromConnectionId,
            payload = request.Payload
        });
        return Ok();
    }

    /// <summary>
    /// Broadcast to all connected clients.
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest request)
    {
        await backplane.Clients.AllAsync(request.Payload);
        return Ok();
    }
}

public record JoinGroupRequest(Guid ConnectionId, string Group);
public record LeaveGroupRequest(Guid ConnectionId, string Group);
public record SendToGroupRequest(Guid ConnectionId, string Group, object Payload);
public record SendToClientRequest(Guid FromConnectionId, Guid TargetConnectionId, object Payload);
public record BroadcastRequest(object Payload);
```

### 3. Connect from the browser

```typescript
const BASE_URL = "http://localhost:5000";
let connectionId: string | null = null;

// 1. Open SSE connection
const es = new EventSource(`${BASE_URL}/api/realtime/connect`);

// 2. Receive connectionId
es.addEventListener("connected", (e) => {
    const data = JSON.parse(e.data);
    connectionId = data.connectionId;
    console.log("Connected:", connectionId);
});

// 3. Join a group (after connected)
async function joinGroup(group: string) {
    await fetch(`${BASE_URL}/api/realtime/groups/join`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ connectionId, group })
    });

    // Listen for events on this group
    es.addEventListener(group, (e) => {
        console.log(`[${group}]`, JSON.parse(e.data));
    });
}

// 4. Send to a group
async function sendToGroup(group: string, message: string) {
    await fetch(`${BASE_URL}/api/realtime/groups/send`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ connectionId, group, payload: { message } })
    });
}

// 5. Send directly to another client
async function sendToClient(targetConnectionId: string, message: string) {
    await fetch(`${BASE_URL}/api/realtime/clients/send`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fromConnectionId: connectionId, targetConnectionId, payload: { message } })
    });
}
```

## Backplane API

### Clients (Publishing)

```csharp
// Broadcast to all
await backplane.Clients.AllAsync(data);

// Send to group
await backplane.Clients.GroupAsync("room-1", data);

// Send to multiple groups
await backplane.Clients.GroupsAsync(["room-1", "room-2"], data);

// Send to specific client
await backplane.Clients.ClientAsync(connectionId, data);

// Send to multiple clients
await backplane.Clients.ClientsAsync([id1, id2], data);
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

## Example App

See `ExampleApp.Chat` for a complete implementation with React client.

## License

MIT
