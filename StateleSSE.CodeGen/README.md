# StateleSSE.CodeGen

Q) What is this?

A) Type-safe EventSource client generation from OpenAPI specifications.

When using contract-based development where OpenAPI becomes the source of truth for type-safe HTTP communication, EventSource client generation is not supported by major frameworks.

This library generates type-safe EventSource clients from OpenAPI specs, supporting:
- Single-event subscriptions
- Multi-event subscriptions with typed event listeners (fluent API)
- No magic strings in source code
- Minimal boilerplate

## Usage

Install to an existing .NET application with CLI:

```bash
dotnet add package StateleSSE.CodeGen
```

For triggering the source generation, we first need at least one valid endpoint.

The generator looks for GET endpoints in an OpenAPI JSON spec.

**Multi-event endpoint:**
```csharp
[HttpGet("events")]
[Produces("text/event-stream")]
[ProducesResponseType(typeof(ChatEventUnion), 200)]
public async Task StreamChatEvents(string roomId)
{
    var eventTypes = new[] { typeof(MessageReceivedEvent), typeof(UserJoinedEvent) };
    await HttpContext.StreamSseAsync(backplane, $"chat:{roomId}", eventTypes);
}

public class ChatEventUnion
{
    public MessageReceivedEvent? MessageReceived { get; set; }
    public UserJoinedEvent? UserJoined { get; set; }
}
```

**Single-event endpoint:**
```csharp
[HttpGet("StreamMessages")]
[Produces("text/event-stream")]
[ProducesResponseType(typeof(MessageReceivedEvent), 200)]
public async Task StreamMessages(string roomId)
{
    await HttpContext.StreamSseAsync<MessageReceivedEvent>(backplane, $"chat:{roomId}");
}
```


### TypeScript Client

```csharp
using StateleSSE.CodeGen;
//Trigger the code generation like this - I like to do this after writing the openapi.json spec to my file system using NSwag.
TypeScriptEventSourceGenerator.Generate(
    openApiSpecPath: "path/to/openapi.json",
    outputPath: "client/src/generated-sse-client.ts",
    baseUrlImport: "./utils/BASE_URL",  // Will target an exported const (string) BASE_URL (example: "http://localhost:5000" or some production URL)
    modelsImport: "./generated-client.ts"  // Optional: Import path for model types. When provided, generates type-safe functions without generics
);
```

#### Usage Examples

**Multi-event endpoint (fluent API):**
```typescript
// Generated for endpoints with ChatEventUnion return type
streamChatEvents('room-123')
    .onMessageReceived((msg) => console.log('Message:', msg.Content))
    .onUserJoined((user) => console.log('Joined:', user.Username))
    .onError((err) => console.error('Error:', err));
```

**Single-event with type imports (when modelsImport is provided):**
```typescript
const es = streamMessages(
    'room-123',
    (msg) => console.log('Received:', msg),  // msg is typed as MessageReceivedEvent
    (err) => console.error('Error:', err)
);

es.close();
```

**Single-event with generics (when modelsImport is not provided):**
```typescript
const es = streamMessages<MessageReceivedEvent>(
    'room-123',
    (msg) => console.log('Received:', msg),
    (err) => console.error('Error:', err)
);

es.close();
```

**Manual subscription (if you prefer):**
```typescript
const stream = new EventSource('/events?roomId=room-123');

stream.addEventListener('MessageReceivedEvent', (e) => {
    const data = JSON.parse((e as MessageEvent).data);
    console.log('Message:', data);
});

stream.addEventListener('UserJoinedEvent', (e) => {
    const data = JSON.parse((e as MessageEvent).data);
    console.log('User joined:', data);
});
```


### C# Client

I made a C# client because I wanted it to be Blazor compatible. I've tested with C# WASM, but it should also be compatible with server side .NET development (maybe useful for testing if you require actual network connection in testing)

```csharp
using StateleSSE.CodeGen;

//Trigger the code generation like this - I like to do this after writing the openapi.json spec to my file system using NSwag.
CSharpEventSourceGenerator.Generate(
    openApiSpecPath: "path/to/openapi.json",
    outputPath: "Generated/SseClient.cs",
    className: "SseClient",           // optional, defaults to this
    namespaceName: "GeneratedClient"  // optional, defaults to this
);
```

C#: (Tested with Blazor WASM)
```csharp
//Consume it in a client application like this:
var httpClient = new HttpClient(
    //your http client config
    );
var client = new SseClient("https://api.example.com", httpClient);

await foreach (var message in client.StreamMessagesAsync("room-123"))
{
    Console.WriteLine($"Received: {message.Data.Content}");
}
```


## Dependencies

No NuGet packages required. Only uses built-in stuff like System.Text.Json
