using System.ComponentModel.DataAnnotations;
using System.Text;
using api.Etc;
using api.Extensions;
using api.Models;
using api.Repositories;
using api.Repositories.Abstractions;
using api.Services;
using api.Services.Abstractions;
using dataaccess;
// using Hangfire;
// using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using mqtt;
using StackExchange.Redis;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using StateleSSE.CodeGen;

namespace api;

public class Program
{
    public static void ConfigureServices(IServiceCollection services, 
        IConfiguration? configuration = null)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        var appOptions = new MyAppOptions();
        configuration?.GetSection(nameof(MyAppOptions)).Bind(appOptions);
        services.AddSingleton(appOptions);

        services.AddCors();

        // JWT Authentication for teaching examples
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.ASCII.GetBytes("TeachingExampleSecretKey_MinimumLength32Characters_ForJWT")),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                // For SignalR: Read JWT from query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/todoHubV2"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // SSE Backplane - use Redis if configured, otherwise InMemory
        if (!string.IsNullOrWhiteSpace(appOptions.Redis))
        {
            services.AddRedisSseBackplane(options =>
            {
                options.RedisConnectionString = appOptions.Redis;
                options.ChannelPrefix = "kahoot";
            });
        }
        else
        {
            services.AddInMemorySseBackplane();
        }

        services.AddSingleton<IMqttService, MqttService>();
       
        services.AddDbContext<MyDbContext>((serviceProvider, options) =>
        {
            options.EnableSensitiveDataLogging();
            options.UseNpgsql(serviceProvider.GetRequiredService<MyAppOptions>().Db);
            
                 options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }, ServiceLifetime.Transient);
        services.AddControllers();

        // Use built-in .NET 10 OpenAPI support
        services.AddOpenApi(options =>
        {
        });
        // Hangfire for distributed background jobs
        // services.AddHangfire(config => config
        //     .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(appOptions.Db)));
        // services.AddHangfireServer(options => { options.WorkerCount = 1; });

        // services.AddScoped<RoundEndJob>();
        // services.AddSingleton<IRealtimeBackplane, RedisBackplane>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<ISeeder, Seeder>();

        // Repositories
        services.AddScoped<IQuizRepository, QuizRepository>();
    }

    public static async Task Main()
    {
        var builder = WebApplication.CreateBuilder();

        // Configure forced shutdown on Ctrl+C
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(1); // Force shutdown after 1 second
        });

        // Configure Kestrel shutdown timeout
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
            serverOptions.AddServerHeader = false;
        });


        // This will catch issues like "Singleton depending on Scoped" at startup
        if (builder.Environment.IsDevelopment())
            builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true; // Detect scoped services resolved from root
                options.ValidateOnBuild = true; // Validate on application startup
            });

        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();

        var appOptions = app.Services.GetRequiredService<MyAppOptions>();
        Validator.ValidateObject(appOptions, new ValidationContext(appOptions), true);
        app.MapControllers();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        // var backplane = app.Services.GetRequiredService<IRealtimeBackplane>();
        // logger.LogInformation(
        //     "Realtime system initialized (server-push mode) with {BackplaneType}",
        //     backplane.GetType().Name);

        app.UseCors(config => config
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials());

        using (var scope = app.Services.CreateScope())
        {
            // var interceptor = app.Services.GetRequiredService<RealtimeChangeInterceptor>();
            // interceptor.SkipProcessing = true;

            var seeder = scope.ServiceProvider.GetRequiredService<ISeeder>();
            await seeder.SeedAsync();

            // interceptor.SkipProcessing = false;
        }


        var mqttService = app.Services.GetRequiredService<IMqttService>();
        await mqttService.ConnectAsync(appOptions.MQTT_BROKER, appOptions.MQTT_USERNAME, appOptions.MQTT_PASS);
        await mqttService.SubscribeAsync(Constants.MqttTopics.WeatherData);

        // Register MQTT message handler for weather data
        mqttService.RegisterHandler(Constants.MqttTopics.WeatherData, async (topic, payload) =>
        {
            try
            {
                var weatherData = System.Text.Json.JsonSerializer.Deserialize<api.Models.BrokerToServerDtos.WeatherDataDto>(payload);
                if (weatherData == null)
                {
                    logger.LogWarning("Failed to deserialize weather data from MQTT: {Payload}", payload);
                    return;
                }

                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                var backplane = scope.ServiceProvider.GetRequiredService<ISseBackplane>();

                // Ensure weather station exists
                var station = await dbContext.WeatherStations
                    .Where(s => s.Id == weatherData.StationId)
                    .FirstOrDefaultAsync();

                if (station == null)
                {
                    station = new WeatherStation
                    {
                        Id = weatherData.StationId,
                        Name = weatherData.StationId
                    };
                    dbContext.WeatherStations.Add(station);
                    await dbContext.SaveChangesAsync();
                }

                // Create weather reading
                var reading = new WeatherReading
                {
                    Id = Guid.NewGuid().ToString(),
                    Stationid = weatherData.StationId,
                    Temperature = (decimal)weatherData.Temperature,
                    Humidity = (decimal)weatherData.Humidity,
                    Pressure = (decimal)weatherData.Pressure,
                    Timestamp = weatherData.Timestamp
                };

                dbContext.WeatherReadings.Add(reading);
                await dbContext.SaveChangesAsync();

                // Publish to SSE clients via StateleSSE
                var weatherEvent = new api.Models.SseEventDtos.WeatherDataEvent(
                    StationId: weatherData.StationId,
                    StationName: station.Name,
                    Temperature: (decimal)weatherData.Temperature,
                    Humidity: (decimal)weatherData.Humidity,
                    Pressure: (decimal)weatherData.Pressure,
                    Timestamp: weatherData.Timestamp
                );

                await backplane.PublishToGroup($"weather:{weatherData.StationId}", weatherEvent);
                await backplane.PublishToGroup("weather:all", weatherEvent);

                logger.LogInformation("Processed weather data from station {StationId}: {Temp}°C, {Humidity}%, {Pressure}hPa",
                    weatherData.StationId, weatherData.Temperature, weatherData.Humidity, weatherData.Pressure);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing MQTT weather data: {Message}", ex.Message);
            }
        });

        app.MapOpenApi();

        // Generate NSwag TypeScript client
        app.GenerateApiClientsFromOpenApi("/../../client/src/generated-client.ts").GetAwaiter().GetResult();

        // Generate EventSource TypeScript client
        TypeScriptEventSourceGenerator.Generate(
            openApiSpecPath: Path.Combine(Directory.GetCurrentDirectory(), "openapi-with-docs.json"),
            outputPath: Path.Combine(Directory.GetCurrentDirectory(), "../../client/src/generated-sse-client.ts")
        );

        await app.RunAsync();
    }
}
