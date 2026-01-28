using System.Text.Json;
using server;
using StackExchange.Redis;
using StateleSSE.AspNetCore;
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

builder.Services.AddRedisSseBackplane();
builder.Services.AddOpenApiDocument(config =>
{
    config.AddStringConstants<MyConstants>();
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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
var backplane = app.Services.GetRequiredService<ISseBackplane>();                                                                                                                         
backplane.OnClientDisconnected += async (_, e) =>                                                                                                                                         
{                                                                                                                                                                                         
    foreach (var group in e.Groups)                                                                                                                                                       
    {                                                                                                                                                                                     
        await backplane.Clients.SendToGroupAsync(group, new UserLeftResponseDto(e.ConnectionId));                                                                                        
    }                                                                                                                                                                                     
};    

app.Run();

public record UserLeftResponseDto(string ConnectionId) : BaseResponseDto;