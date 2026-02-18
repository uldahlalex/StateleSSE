using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Mqtt.Controllers;
using NSwag;
using NSwag.Generation.Processors.Security;
using server;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.GroupRealtime;
using Testcontainers.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var connectionStrings = new ConnectionStrings();
configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);
if (string.IsNullOrWhiteSpace(connectionStrings.DbConnectionString))
{
    var container = new PostgreSqlBuilder("postgres:15.1").Build();
    container.StartAsync().GetAwaiter().GetResult();
    connectionStrings.DbConnectionString = container.GetConnectionString();
}

builder.Services.AddSingleton(connectionStrings);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(connectionStrings.Secret))
    });
builder.Services.AddAuthorization();
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(0));

builder.Services.AddInMemorySseBackplane();
builder.Services.AddEfRealtime();
builder.Services.AddGroupRealtime();


builder.Services.AddDbContext<MyDbContext>((sp, conf) =>
{
    conf.UseNpgsql(connectionStrings.DbConnectionString);
    conf.AddEfRealtimeInterceptor(sp);
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
    
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var exception = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception != null)
        {
            context.ProblemDetails.Detail = exception.Message;
        }
    };
});
builder.Services.AddSingleton<JwtService>();
builder.Services.AddMqttControllers();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddCors();
builder.Services.AddScoped<Seeder>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseOpenApi();
app.UseSwaggerUi();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseStaticFiles();
app.UseCors(c => 
    c.AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin()
        .SetIsOriginAllowed(_ => true));


 var mqttClient = app.Services.GetRequiredService<IMqttClientService>();
 await mqttClient.ConnectAsync(connectionStrings.MqttBroker, connectionStrings.MqttPort, "", "");
app.GenerateApiClientsFromOpenApi("../client/src/generated-ts-client.ts", "./openapi.json").GetAwaiter().GetResult();

using(var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<Seeder>().Seed();
}

app.Run();

