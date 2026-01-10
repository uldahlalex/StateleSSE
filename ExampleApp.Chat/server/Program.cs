using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.CodeGen;
using StateleSSE.Backplane.Redis.Extensions;

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
// Generate TypeScript EventSource client
TypeScriptSseGenerator.Generate(
    openApiSpecPath: "openapi-with-docs.json",
    outputPath: "../client/src/generated-sse-client.ts"
);

app.Run();
