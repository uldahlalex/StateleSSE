using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StateleSSE.AspNetCore.Infrastructure;

namespace StateleSSE.AspNetCore.Extensions;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresSseBackplane(
        this IServiceCollection services,
        Action<PostgresSseBackplaneOptions> configure)
    {
        var options = new PostgresSseBackplaneOptions();
        configure(options);

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PostgresBackplane>>();
            var jsonOptions = sp.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
            return new PostgresBackplane(options.ConnectionString, logger, options.ConnectionTtl, jsonOptions);
        });

        services.AddSingleton<ISseBackplane>(sp => sp.GetRequiredService<PostgresBackplane>());

        return services;
    }
}

public class PostgresSseBackplaneOptions
{
    public string ConnectionString { get; set; } = "Host=localhost;Database=postgres";
    public TimeSpan? ConnectionTtl { get; set; }
}
