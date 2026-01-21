# StateleSSE.AspNetCore

## What is it?

Type-safe, horizontally-scalable Server-Sent Events (SSE) framework for ASP.NET Core.

Built for multi-client realtime web apps that scale. Features single-connection multi-event pattern to solve browser connection limits.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## Usage

### Step 1: Add Backplane to DI

Here demonstrated with a very basic Program.cs startup pipeline:

```csharp
//using StackExchange.Redis; remove comment and have the StackExchange.Redis nuget package installed if you want to use redis for backplane instead of inmemory
using StateleSSE.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
/* this is required if you want to use redis for backplane - here im simply using a local redis db
   builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    { var config = ConfigurationOptions.Parse( "localhost:6379" );
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config); });*/
builder.Services.AddInMemorySseBackplane();
//builder.Services.AddRedisSseBackplane(); Use this one instead if you want to use redis for backplane - comment out the inmemorybackplane
builder.Services.AddControllers(); //You can also use minimal API or other API type - the library has no MVC dependency
var app = builder.Build();
app.MapControllers();
app.Run();
```

### Step 2: Set up endpoints

**Single-connection multi-event pattern (recommended):**

```csharp
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

[ApiController]
public class ChatController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet("events")]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(ChatEventUnion), 200)]
    public async Task StreamChatEvents(string roomId)
    {
        var channel = $"chat:{roomId}";
        var eventTypes = new[]
        {
            typeof(MessageReceivedEvent),
            typeof(UserJoinedEvent),
            typeof(UserLeftEvent)
        };
        await HttpContext.StreamSseAsync(backplane, channel, eventTypes);
    }

    [HttpPost("messages")]
    public async Task SendMessage([FromBody] SendMessageRequest request)
    {
        var channel = $"chat:{request.RoomId}";
        var evt = new MessageReceivedEvent(request.Username, request.Content, DateTime.UtcNow);
        await backplane.PublishToGroup(channel, evt);
    }
}

public record MessageReceivedEvent(string Username, string Content, DateTime Timestamp);
public record UserJoinedEvent(string Username, DateTime Timestamp);
public record UserLeftEvent(string Username, DateTime Timestamp);

public class ChatEventUnion
{
    public MessageReceivedEvent? MessageReceived { get; set; }
    public UserJoinedEvent? UserJoined { get; set; }
    public UserLeftEvent? UserLeft { get; set; }
}

public record SendMessageRequest(string Username, string Content, string RoomId);
```

**Alternative: Single event type endpoint:**

```csharp
[HttpGet(nameof(StreamMessages))]
[Produces("text/event-stream")]
[ProducesResponseType(typeof(MessageReceivedEvent), 200)]
public async Task StreamMessages(string roomId)
{
    var channel = $"chat:{roomId}";
    await HttpContext.StreamSseAsync<MessageReceivedEvent>(backplane, channel);
}
```

### Step 3: Client usage

**Browser (JavaScript/TypeScript):**
```typescript
const stream = new EventSource('/events?roomId=room1');

stream.addEventListener('MessageReceivedEvent', (e) => {
    const data = JSON.parse((e as MessageEvent).data);
    console.log(`${data.Username}: ${data.Content}`);
});

stream.addEventListener('UserJoinedEvent', (e) => {
    const data = JSON.parse((e as MessageEvent).data);
    console.log(`${data.Username} joined`);
});
```

**cURL (testing):**
```bash
# Terminal 1: Subscribe to the SSE stream
curl -N http://localhost:5000/events?roomId=room1

# Terminal 2: Send a message (will appear in Terminal 1)
curl -X POST http://localhost:5000/messages \
    -H "Content-Type: application/json" \
    -d '{"Username":"Alice","Content":"Hello!","RoomId":"room1"}'
```

## System vision

I'd like to share my views on why I have designed the system this way:

A lot of real-time frameworks have the following characteristics which I dislike:
- Client-side management of connection (with things like WebSockets which can be open, closed, opening, etc...)
- Weak and unopinionated "endpoint" / orchestration of communication. (like all network traffic all arriving to a single point in the client app, which now has to mediate traffic)
- Bad support for web documentation standards / no living docs (no swagger/openapi, lack of source generators based around this standard. AsyncAPI is mostly geared towards "broker" oriented stuff )
- Horizontal scaling and server side connection / user management can be very difficult (SignalR has a decent "Users/Connections/Groups" abstraction, which I'm inspired by)

Most web devs have existing familiarity with request-response pattern in a simple client-server app with HTTP.
The concept of this framework is: Custom HTTP endpoints that simply lets clients subscribe to a broadcast / stream and let them know which DTO they will receive upon that event.


## CodeGen for Typescript & C# Client Code

Can be found in StateleSSE.CodeGen at https://github.com/uldahlalex/StateleSSE.CodeGen 

Explained shortly: Use an OpenAPI generator like NSwag/Swashbuckle/etc to get a JSON spec. Use this JSON file to generate relevant client code for type-safe communication with your realtime API.

## License

MIT
