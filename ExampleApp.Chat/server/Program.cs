using api.Etc;
using Microsoft.AspNetCore.TestHost;
using StateleSSE.AspNetCore;
using StateleSSE.CodeGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInMemorySseBackplane();
builder.Services.AddOpenApiDocument();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new server.SseEndpointConvention());
});

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi();
app.MapControllers();
app.MapOpenApi();

var currentDir = Directory.GetCurrentDirectory();
var nswagSpecPath = Path.Combine(currentDir, "openapi-with-docs.json");
var aspNetCoreSpecPath = Path.Combine(currentDir, "openapi-spec.json");
var clientPath = Path.Combine(currentDir, "../client/src/generated-client.ts");
var sseClientPath = Path.Combine(currentDir, "../client/src/generated-sse-client.ts");

await app.GenerateApiClientsFromOpenApi(clientPath, nswagSpecPath);

var testBuilder = WebApplication.CreateBuilder(args);
testBuilder.WebHost.UseTestServer();
testBuilder.Services.AddInMemorySseBackplane();
testBuilder.Services.AddOpenApi();
testBuilder.Services.AddControllers();

using var testApp = testBuilder.Build();
testApp.MapControllers();
testApp.MapOpenApi();
await testApp.StartAsync();

using var client = testApp.GetTestClient();
var aspNetCoreSpec = await client.GetStringAsync("/openapi/v1.json");
await File.WriteAllTextAsync(aspNetCoreSpecPath, aspNetCoreSpec);

TypeScriptEventSourceGenerator.Generate(
    openApiSpecPath: aspNetCoreSpecPath,
    outputPath: sseClientPath,
    baseUrlImport: "./utils/BASE_URL"
);

app.Run();
