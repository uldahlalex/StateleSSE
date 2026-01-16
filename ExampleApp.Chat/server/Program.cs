using api.Etc;
using StackExchange.Redis;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using StateleSSE.CodeGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(3));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var config = ConfigurationOptions.Parse(
            //docker redis localhost
            "localhost:6379"
            );
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config);
    });

// builder.Services.AddRedisSseBackplane();
builder.Services.AddInMemorySseBackplane();

builder.Services.AddOpenApiDocument();
builder.Services.AddControllers();
builder.Services.AddCors();

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi();
app.MapControllers();
app.UseCors(c => c.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin()
    .SetIsOriginAllowed(_ => true));


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
