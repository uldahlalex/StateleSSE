using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using api.Etc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;
using server;
using StackExchange.Redis;
using Microsoft.AspNetCore.Diagnostics;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
var db = builder.Configuration.GetSection("DbConnectionString").Value;
var redis = builder.Configuration.GetSection("Redis").Value;
var secret = builder.Configuration.GetSection("Secret").Value;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    });
builder.Services.AddAuthorization();
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(0));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse( redis    );
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddRedisSseBackplane();
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<MyDbContext>((sp, conf) =>
{
    conf.UseNpgsql(db);
    conf.AddEfRealtimeInterceptor(sp); // hooks into SaveChanges

});
builder.Services.AddOpenApiDocument(config =>
{
    config.AddSecurity("Bearer", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token"
    });
    config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    config.AddStringConstants<MyConstants>();

});

builder.Services.AddSingleton<JwtService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddCors();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler(config => { });
app.UseCors(c =>
    c.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin()
    .SetIsOriginAllowed(_ => true));
app.UseOpenApi();
app.UseSwaggerUi();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


app.GenerateApiClientsFromOpenApi("../client/src/generated-ts-client.ts", "./openapi.json").GetAwaiter().GetResult();
var backplane = app.Services.GetRequiredService<ISseBackplane>();                                                                                                                         
backplane.OnClientDisconnected += async (_, e) =>                                                                                                                                         
{                                                                                                                                                                                         
                                                                                                                                                                                    
        await backplane.Clients.SendToGroupAsync("user-left", new UserLeftResponseDto(e.ConnectionId));                                                                                        
                                                                                                                                                    
};
using (var scope = app.Services.CreateScope())
{
 var ctx =   scope.ServiceProvider.GetRequiredService<MyDbContext>();
 Console.WriteLine(ctx.Database.GenerateCreateScript());

 ctx.Database.EnsureCreated();
 var exists = ctx.Users.Any(u => u.Id == "test");
 if (!exists)
 {
     
     var salt = "word";
     var password = "pass";
     ctx.Users.Add(new User()
     {
         Id = Guid.NewGuid().ToString(),
         Nickname = "test",
         //password is "pass", salt is "word"
         Hash = Convert.ToBase64String(
             System.Security.Cryptography.SHA256.HashData(
                 System.Text.Encoding.UTF8.GetBytes(password + salt))),
         Salt = salt,
     });
     ctx.SaveChanges();
 }
}

app.Run();

public record UserLeftResponseDto(string ConnectionId) : BaseResponseDto;