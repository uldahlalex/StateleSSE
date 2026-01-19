# StateleSSE.CodeGen

Q) What is this?

A) When using contract-based development style where OpenAPI spec becomes the source of truth for typesafe http communication (like using NSwag codegeneration, etc).

However, making EventSource client communication is not supported with major frameworks.

This library uses an existing OpenAPI JSON spec to create event source client syntax so you don't need to have magic strings in your source code (and minimizes boilerplate).

## Usage

Install to an existing .NET application with CLI:

```bash
dotnet add package StateleSSE.CodeGen
```

For triggering the source generation, we first need at least one valid endpoint.

The generator looks for GET endpoints in an OpenAPI JSON spec. So for example, this controller GET method:

```csharp
[HttpGet("StreamMessages")]
public async Task StreamMessages(string groupId)
{
    await StreamEventType<Message>($"chat:{groupId}:Message"); //This method comes from the StateleSSE.AspNetCore package. This package does not technically require that Nuget, but I don't think anyone would use this CodeGen without the StateleSSE.AspNetCore
}
```


### TypeScript Client

```csharp
using StateleSSE.CodeGen;
//Trigger the code generation like this - I like to do this after writing the openapi.json spec to my file system using NSwag.
TypeScriptEventSourceGenerator.Generate(
    openApiSpecPath: "path/to/openapi.json",
    outputPath: "client/src/generated-sse-client.ts",
    baseUrlImport: "./utils/BASE_URL"  // Will target an exported const (string) BASE_URL (example: "http://localhost:5000" or some production URL)
);
```

#### Usage Examples

TypeScript:
```typescript
//In your TS client app, use the generated streamMessages<T>(params, onMessage, onError) method like this:
const es = streamMessages<Message>(
    "room-123",
    (msg) => console.log("Received:", msg),
    (err) => console.error("Error:", err)
);

es.close();
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
