using api.Etc;
using StateleSSE.AspNetCore;
using StateleSSE.CodeGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInMemorySseBackplane();
builder.Services.AddOpenApiDocument();
builder.Services.AddControllers();

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi();
app.MapControllers();

var currentDir = Directory.GetCurrentDirectory();
var openApiSpecPath = Path.Combine(currentDir, "openapi.json");
var clientPath = Path.Combine(currentDir, "../client/src/generated-client.ts");
var sseClientPath = Path.Combine(currentDir, "../client/src/generated-sse-client.ts");

await app.GenerateApiClientsFromOpenApi(clientPath, openApiSpecPath);

TypeScriptEventSourceGenerator.Generate(
    openApiSpecPath: openApiSpecPath,
    outputPath: sseClientPath,
    baseUrlImport: "./utils/BASE_URL"
);

app.Run();
