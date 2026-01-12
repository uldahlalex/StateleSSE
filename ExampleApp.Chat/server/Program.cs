using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using StateleSSE.CodeGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInMemorySseBackplane();

builder.Services.AddOpenApi(conf =>
{
    conf.AddOperationTransformer<MicrosoftOpenApiEventSourceTransformer>();
});
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.MapOpenApi();

// Start the app in the background to generate OpenAPI spec
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Wait for app to start

    try
    {
        using var client = new HttpClient();
        var spec = await client.GetStringAsync("http://localhost:5000/openapi/v1.json");

        var openApiPath = Path.Combine(Directory.GetCurrentDirectory(), "openapi-spec.json");
        await File.WriteAllTextAsync(openApiPath, spec);

        // Generate TypeScript EventSource client
        TypeScriptEventSourceGenerator.Generate(
            openApiSpecPath: openApiPath,
            outputPath: Path.Combine(Directory.GetCurrentDirectory(), "../client/src/generated-sse-client.ts")
        );

        Console.WriteLine("✅ Generated TypeScript SSE client");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Failed to generate TypeScript client: {ex.Message}");
    }
});

app.Run();
