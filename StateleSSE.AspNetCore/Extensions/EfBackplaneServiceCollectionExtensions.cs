using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StateleSSE.AspNetCore.EfBackplane;

namespace StateleSSE.AspNetCore.Extensions;

public static class EfBackplaneServiceCollectionExtensions
{
    public static IServiceCollection AddEfSseBackplane<TContext>(this IServiceCollection services)
        where TContext : SseDbContext
    {
        services.AddSingleton(sp =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EfSseBackplane<TContext>>>();
            var jsonOptions = sp.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
            return new EfSseBackplane<TContext>(scopeFactory, logger, jsonOptions);
        });
        services.AddSingleton<ISseBackplane>(sp => sp.GetRequiredService<EfSseBackplane<TContext>>());
        return services;
    }
}
