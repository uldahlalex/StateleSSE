using api.Etc;
using StackExchange.Redis;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using StateleSSE.CodeGen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(0));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var config = ConfigurationOptions.Parse(
            "localhost:6379"
            );
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config);
    });

// builder.Services.AddInMemorySseBackplane();
builder.Services.AddRedisSseBackplane();
builder.Services.AddOpenApiDocument();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddCors();

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi();
app.MapControllers();
app.UseCors(c => 
    c.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin()
    .SetIsOriginAllowed(_ => true));

var currentDir = Directory.GetCurrentDirectory();
var openApiSpecPath = Path.Combine(currentDir, "openapi.json");
var clientPath = Path.Combine(currentDir, "../client/src/generated-client.ts");
var sseClientPath = Path.Combine(currentDir, "../client/src/generated-sse-client.ts");
var csharpClientPath = Path.Combine(currentDir, "../BlazorClient/Generated/ChatApiClient.cs");
var csharpSseClientPath = Path.Combine(currentDir, "../BlazorClient/Generated/ChatSseClient.cs");

await app.GenerateApiClientsFromOpenApi(clientPath, openApiSpecPath, csharpClientPath);

TypeScriptEventSourceGenerator.Generate(
    openApiSpecPath: openApiSpecPath,
    outputPath: sseClientPath,
    baseUrlImport: "./utils/BASE_URL",
    modelsImport: "./generated-client.ts"
);

CSharpEventSourceGenerator.Generate(
    openApiSpecPath: openApiSpecPath,
    outputPath: csharpSseClientPath,
    className: "ChatSseClient",
    namespaceName: "ChatApi.Client"
);

app.Run();
