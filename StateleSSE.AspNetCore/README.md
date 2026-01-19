# StateleSSE.AspNetCore

## What is it?

Standardized client/group management for server sent events with horizontal scaling & more.

Built for multi-client realtime web apps that scale (with Server Sent Events (SSE)).

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

*here demo'ed with a controller with a "subscribe/stream" and one broadcast method:*

```csharp
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;

public class ChatController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet(nameof(StreamMessages))]
    [Produces<Message>]
    public async Task StreamMessages(string groupId)
    {
        var channel = $"chat:{groupId}:Message";
        await HttpContext.StreamSseAsync<Message>(backplane, channel);
    }

    [HttpPost(nameof(CreateMessage))]
    public async Task CreateMessage([FromBody] CreateMessageRequest request)
    {
        var channel = $"chat:{request.GroupId}:Message";
        var message = new Message { Content = request.Content };
        await backplane.PublishToGroup(channel, message);
    }
}

public class Message
{
    public required string Content { get; set; }
}

public record CreateMessageRequest(string Content, string GroupId);

```

### Step 3: Profit:

```bash
#Start the API before running the cURL commands - here I'm just assuming port 5000 is used
#Terminal 1: Subscribe to the SSE stream
curl -N http://localhost:5000/StreamMessages?groupId=room1

# Terminal 2: Send a message (will appear in Terminal 1)
curl -X POST http://localhost:5000/CreateMessage \
    -H "Content-Type: application/json" \
    -d '{"Content":"Hello from curl!","GroupId":"room1"}'
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
