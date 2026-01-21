# Single-Connection Multi-Event Pattern

StateleSSE now supports streaming multiple event types over a single EventSource connection using the SSE `event:` field. This solves browser connection limit problems and reduces server overhead.

## The Problem

**Old Approach: 5 connections for 5 event types**
```typescript
const stream1 = new EventSource('/RoundStartedEvent?gameId=123');
const stream2 = new EventSource('/PlayerJoinedEvent?gameId=123');
const stream3 = new EventSource('/AnswerSubmittedEvent?gameId=123');
const stream4 = new EventSource('/RoundEndedEvent?gameId=123');
const stream5 = new EventSource('/GameCreatedEvent?gameId=123');
```

**Issues:**
- Browser connection limit: 6 per domain (HTTP/1.1)
- Server overhead: 5x connections, 5x keepalives, 5x memory
- Multiple tabs break (Tab 2 can't open all streams)

## The Solution

**New Approach: 1 connection for 5 event types**
```typescript
const stream = new EventSource('/all?gameId=123');

stream.addEventListener('RoundStartedEvent', (e) => {
    const data: RoundStartedEvent = JSON.parse((e as MessageEvent).data);
    console.log('Round started:', data);
});

stream.addEventListener('PlayerJoinedEvent', (e) => {
    const data: PlayerJoinedEvent = JSON.parse((e as MessageEvent).data);
    console.log('Player joined:', data);
});
```

**Benefits:**
- 5x fewer connections (1 instead of 5)
- 5x less server memory/CPU
- Multiple tabs work reliably
- Full type safety maintained

## Server-Side Implementation

### 1. Controller with Multi-Event Endpoint

```csharp
using StateleSSE.AspNetCore;

[ApiController]
public class GameEventsController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet("all")]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(GameEventUnion), 200)]
    public async Task StreamAllGameEvents([FromQuery] string gameId)
    {
        var channel = $"game:{gameId}";
        var eventTypes = new[]
        {
            typeof(RoundStartedEvent),
            typeof(PlayerJoinedEvent),
            typeof(AnswerSubmittedEvent),
            typeof(RoundEndedEvent),
            typeof(GameCreatedEvent)
        };
        await HttpContext.StreamSseAsync(backplane, channel, eventTypes);
    }
}
```

### 2. Publishing Events

Publishing remains unchanged. Publish to a single channel:

```csharp
await backplane.PublishToGroup($"game:{gameId}", new RoundStartedEvent(...));
await backplane.PublishToGroup($"game:{gameId}", new PlayerJoinedEvent(...));
await backplane.PublishToGroup($"game:{gameId}", new AnswerSubmittedEvent(...));
```

All subscribers to `game:{gameId}` receive all events, but the streaming endpoint filters by event type.

## Client-Side Usage

### TypeScript Example

```typescript
import { BASE_URL } from './utils/BASE_URL';
import type {
    RoundStartedEvent,
    PlayerJoinedEvent,
    AnswerSubmittedEvent
} from './generated-client';

function subscribeToGameEvents(gameId: string) {
    const stream = new EventSource(`${BASE_URL}/all?gameId=${gameId}`);

    stream.addEventListener('RoundStartedEvent', (e) => {
        const data: RoundStartedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Question:', data.questionText);
        console.log('Time limit:', data.timeLimit);
    });

    stream.addEventListener('PlayerJoinedEvent', (e) => {
        const data: PlayerJoinedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Player joined:', data.userName);
    });

    stream.addEventListener('AnswerSubmittedEvent', (e) => {
        const data: AnswerSubmittedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Answers received:', data.answersReceived);
    });

    stream.onerror = (error) => {
        console.error('Connection error:', error);
    };

    return {
        close: () => stream.close()
    };
}
```

### React Example

```typescript
import { useEffect, useState } from 'react';

function GameComponent({ gameId }: { gameId: string }) {
    const [question, setQuestion] = useState<string | null>(null);
    const [players, setPlayers] = useState<number>(0);

    useEffect(() => {
        const stream = new EventSource(`${BASE_URL}/all?gameId=${gameId}`);

        stream.addEventListener('RoundStartedEvent', (e) => {
            const data = JSON.parse((e as MessageEvent).data);
            setQuestion(data.questionText);
        });

        stream.addEventListener('PlayerJoinedEvent', (e) => {
            const data = JSON.parse((e as MessageEvent).data);
            setPlayers(data.playerCount);
        });

        return () => stream.close();
    }, [gameId]);

    return (
        <div>
            <p>Players: {players}</p>
            {question && <p>Question: {question}</p>}
        </div>
    );
}
```

## How It Works

### SSE Event Field

SSE supports an optional `event:` field to distinguish event types on a single connection:

**Server sends:**
```
event: RoundStartedEvent
data: {"questionText":"What is 2+2?","timeLimit":30}

event: PlayerJoinedEvent
data: {"userName":"Alice","playerCount":5}
```

**Client receives:**
```typescript
stream.addEventListener('RoundStartedEvent', handler1);
stream.addEventListener('PlayerJoinedEvent', handler2);
```

### Implementation Details

StateleSSE now:
1. Adds `event: {TypeName}\n` to all SSE messages
2. Provides `StreamSseAsync(context, backplane, channel, eventTypes[])` overload
3. Filters incoming backplane messages by type
4. Sends each message with its type name as the event field

## TypeScript Code Generation

The TypeScript generator will be updated to detect multi-event endpoints (marked with `GameEventUnion` return type) and generate:

```typescript
export function streamAllGameEvents(gameId: string) {
    const url = `${BASE_URL}/all?gameId=${gameId}`;
    const es = new EventSource(url);

    return {
        eventSource: es,
        onRoundStarted: (callback: (data: RoundStartedEvent) => void) => {
            es.addEventListener('RoundStartedEvent', (e) => {
                const data: RoundStartedEvent = JSON.parse((e as MessageEvent).data);
                callback(data);
            });
            return this;
        },
        onPlayerJoined: (callback: (data: PlayerJoinedEvent) => void) => {
            es.addEventListener('PlayerJoinedEvent', (e) => {
                const data: PlayerJoinedEvent = JSON.parse((e as MessageEvent).data);
                callback(data);
            });
            return this;
        },
        onError: (callback: (error: Event) => void) => {
            es.onerror = callback;
            return this;
        },
        close: () => es.close()
    };
}
```

**Usage:**
```typescript
streamAllGameEvents('123')
    .onRoundStarted((data) => console.log(data.questionText))
    .onPlayerJoined((data) => console.log(data.userName))
    .onError((err) => console.error(err));
```

## Migration Guide

### From Old Pattern (5 connections)

**Before:**
```csharp
[HttpGet(nameof(RoundStartedEvent))]
public async Task StreamRoundStarted([FromQuery] string gameId)
{
    await HttpContext.StreamSseAsync<RoundStartedEvent>(backplane, $"game:{gameId}:RoundStartedEvent");
}

[HttpGet(nameof(PlayerJoinedEvent))]
public async Task StreamPlayerJoined([FromQuery] string gameId)
{
    await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, $"game:{gameId}:PlayerJoinedEvent");
}
```

**After:**
```csharp
[HttpGet("all")]
public async Task StreamAllGameEvents([FromQuery] string gameId)
{
    var eventTypes = new[] { typeof(RoundStartedEvent), typeof(PlayerJoinedEvent) };
    await HttpContext.StreamSseAsync(backplane, $"game:{gameId}", eventTypes);
}
```

**Client Before:**
```typescript
const s1 = new EventSource('/RoundStartedEvent?gameId=123');
const s2 = new EventSource('/PlayerJoinedEvent?gameId=123');
s1.onmessage = (e) => { /* handle */ };
s2.onmessage = (e) => { /* handle */ };
```

**Client After:**
```typescript
const s = new EventSource('/all?gameId=123');
s.addEventListener('RoundStartedEvent', (e) => { /* handle */ });
s.addEventListener('PlayerJoinedEvent', (e) => { /* handle */ });
```

## Best Practices

### When to Use Single-Connection Pattern

Use for:
- Multiple related event types in one feature (e.g., game events)
- High-traffic applications with many concurrent users
- Mobile apps (stricter connection limits)
- Multiple tabs per user

Avoid for:
- Completely independent features (e.g., chat vs stock ticker)
- Single event type subscriptions

### Channel Naming Convention

**Single-connection pattern:**
```csharp
var channel = $"game:{gameId}";
```

**Old pattern (deprecated for multi-event scenarios):**
```csharp
var channel = $"game:{gameId}:RoundStartedEvent";
```

### Event Union Type

Define a union type for OpenAPI/TypeScript generation:

```csharp
public class GameEventUnion
{
    public string Type { get; set; } = string.Empty;
    public GameCreatedEvent? GameCreated { get; set; }
    public PlayerJoinedEvent? PlayerJoined { get; set; }
    public RoundStartedEvent? RoundStarted { get; set; }
}
```

Use in controller:
```csharp
[ProducesResponseType(typeof(GameEventUnion), 200)]
public async Task StreamAllGameEvents([FromQuery] string gameId) { ... }
```

## Performance Comparison

| Metric | Old (5 endpoints) | New (1 endpoint) | Improvement |
|--------|------------------|------------------|-------------|
| Browser connections | 5 | 1 | 80% reduction |
| Server memory (per client) | ~75-300 KB | ~15-60 KB | 80% reduction |
| Keepalive network traffic | 5x messages | 1x messages | 80% reduction |
| Multi-tab support | Breaks at 2 tabs | Works reliably | ∞ |

## HTTP/2 Consideration

With HTTP/2, the browser connection limit is removed (multiplexing). However, single-connection pattern is still beneficial because:
- Reduces server-side overhead (memory, CPU, channels)
- Simplifies client code
- Works on HTTP/1.1 environments
- Reduces keepalive network traffic

## Backward Compatibility

The old single-event-type pattern still works:

```csharp
await HttpContext.StreamSseAsync<RoundStartedEvent>(backplane, channel);
```

Now includes `event:` field automatically, so clients can optionally use:
```typescript
stream.addEventListener('RoundStartedEvent', handler);
```

Instead of:
```typescript
stream.onmessage = handler;
```
