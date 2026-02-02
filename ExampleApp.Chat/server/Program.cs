using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using server;
using StackExchange.Redis;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        IssuerSigningKey = JwtService.Key
    });
builder.Services.AddAuthorization();
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
builder.Services.AddDbContext<MyDbContext>((conf) =>
{
    conf.UseNpgsql("Host=localhost;Database=exampleapp_chat;Username=postgres;Password=postgres");
});
builder.Services.AddOpenApiDocument(config =>
{
    config.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token"
    });
    config.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    config.AddStringConstants<MyConstants>();
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddCors();

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi();
app.UseAuthentication();
app.UseAuthorization();
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
using (var scope = app.Services.CreateScope())
{
 var ctx =   scope.ServiceProvider.GetRequiredService<MyDbContext>();
 ctx.Database.EnsureCreated();
 var exists = ctx.Users.Any(u => u.Id == "test");
 if (!exists)
 {
     ctx.Users.Add(new User()
     {
         Id = "test",
         Nickname = "test"

     });
     ctx.SaveChanges();
 }
}

app.Run();

public record UserLeftResponseDto(string ConnectionId) : BaseResponseDto;