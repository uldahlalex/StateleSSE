using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StateleSSE.AspNetCore.Infrastructure;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for configuring InMemoryBackplane SSE services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds in-memory SSE backplane to the service collection.
    /// Suitable for single-server deployments, development, and testing.
    /// Uses JSON serializer options from AddControllers().AddJsonOptions() if configured.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInMemorySseBackplane(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryBackplane>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InMemoryBackplane>>();
            var jsonOptions = sp.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
            return new InMemoryBackplane(logger, jsonOptions);
        });
        services.AddSingleton<ISseBackplane>(sp => sp.GetRequiredService<InMemoryBackplane>());
        return services;
    }
}
