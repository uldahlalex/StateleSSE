# StateleSSE.AspNetCore

Q) What is this?
A) Making real-time web apps can be challenging. 

A lot of real-time frameworks have the following characteristics which I dislike:
- Client-side management of connection (with things like WebSockets which can be open, closed, opening, etc...)
- Weak and unopinionated "endpoint" / orchestration of communication. (like all network traffic all arriving to a single point in the client app, which now has to mediate traffic)
- Bad support for web documentation standards / no living docs (no swagger/openapi, lack of source generators based around this standard. AsyncAPI is mostly geared towards "broker" oriented stuff )
- Horizontal scaling and server side connection / user management can be very difficult (SignalR has a decent "Users/Connections/Groups" abstraction, which I'm inspired by)

Most web devs have existing familiarity with request-response pattern in a simple client-server app with HTTP.
The concept of this framework is: Custom HTTP endpoints that simply lets clients subscribe to a broadcast / stream and let them know which DTO they will receive upon that event.



## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## Usage

### Step 1: Configure DI

```csharp

```

### Step 2: Set up endpoints

*here demo'ed with a controller with a subscribe and one broadcast:*

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

## What It Handles

- SSE response headers
- Backplane subscription lifecycle
- JSON serialization and SSE formatting
- Cancellation token handling
- Cleanup in finally blocks


## License

MIT
