using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateleSSE.AspNetCore.Infrastructure.EfBackplane;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for configuring the EF-backed SSE backplane.
/// </summary>
public static class EfServiceCollectionExtensions
{
    /// <summary>
    /// Adds EF-backed SSE backplane using <typeparamref name="TDbContext"/> for connection/group persistence.
    /// Single-server only — no horizontal scaling.
    /// <typeparamref name="TDbContext"/> must implement <see cref="ISseEfContext"/> and call
    /// <see cref="SseBackplaneDbContext.ConfigureModel"/> from <c>OnModelCreating</c>.
    /// </summary>
    public static IServiceCollection AddEfSseBackplane<TDbContext>(
        this IServiceCollection services,
        TimeSpan? connectionTtl = null)
        where TDbContext : DbContext, ISseEfContext
    {
        services.AddSingleton(sp =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<EfBackplane<TDbContext>>>();
            var jsonOptions = sp.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
            return new EfBackplane<TDbContext>(scopeFactory, logger, jsonOptions);
        });

        services.AddSingleton<ISseBackplane>(sp => sp.GetRequiredService<EfBackplane<TDbContext>>());

        services.AddSingleton<IHostedService>(sp =>
        {
            var backplane = sp.GetRequiredService<EfBackplane<TDbContext>>();
            var logger = sp.GetRequiredService<ILogger<EfBackplaneHostedService<TDbContext>>>();
            return new EfBackplaneHostedService<TDbContext>(backplane, logger, connectionTtl);
        });

        return services;
    }
}
