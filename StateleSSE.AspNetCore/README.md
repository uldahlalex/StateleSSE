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

```csharp
using StateleSSE.AspNetCore;
using StateleSSE.Abstractions;

[ApiController]
public class GameController(ISseBackplane backplane) : ControllerBase
{
    //"Broadcasting" to that topic
    [HttpGet("events/player-joined")]
    public async Task StreamPlayerJoined([FromQuery] string gameId)
    {
        var channel = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", gameId);
        await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
    }

    //"Subscribing" to a "topic"
    [HttpGet("game/stream")]
    public async Task GameStream([FromQuery] string gameId)
    {
        var channel = 
            backplane, channel, () => GetGameState(gameId), "game_state");
    }
}
```

## Extension Methods

**StreamSseAsync&lt;TEvent&gt;** - Stream typed events
```csharp
await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
```

**StreamSseWithInitialStateAsync&lt;TState&gt;** - Stream with initial state
```csharp
await HttpContext.StreamSseWithInitialStateAsync(
    backplane, channel, getInitialState, eventName);
```

**StreamSseAsync** - Stream untyped events
```csharp
await HttpContext.StreamSseAsync(backplane, channel);
```

## What It Handles

- SSE response headers
- Backplane subscription lifecycle
- JSON serialization and SSE formatting
- Cancellation token handling
- Cleanup in finally blocks

## Channel Naming

```csharp
ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", "123");
// "game:123:PlayerJoinedEvent"

ChannelNamingExtensions.Channel("game", "123");
// "game:123"

ChannelNamingExtensions.BroadcastChannel("game");
// "game:all"
```

## License

MIT
