using api.Etc;
using server;
using StackExchange.Redis;
using StateleSSE.AspNetCore.Extensions;

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
builder.Services.AddOpenApiDocument(config =>
{
    config.AddStringConstants(typeof(ExampleApp.Chat.Channels));
});
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

app.GenerateApiClientsFromOpenApi("../client/src/generated-ts-client.ts", "./openapi.json").GetAwaiter().GetResult();


app.Run();
