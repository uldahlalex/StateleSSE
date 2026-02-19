using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using StateleSSE.AspNetCore.EfRealtime;
using StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

namespace StateleSSE.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring Postgres SSE backplane services.
/// </summary>
public static class PostgresServiceCollectionExtensions
{
    /// <summary>
    /// Adds Postgres-based SSE backplane. Creates its own NpgsqlDataSource from the connection string.
    /// </summary>
    public static IServiceCollection AddPostgresSseBackplane(
        this IServiceCollection services,
        string connectionString,
        TimeSpan? connectionTtl = null)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        return RegisterCoreServices(services, connectionTtl);
    }

    /// <summary>
    /// Adds Postgres-based SSE backplane using an existing <see cref="NpgsqlDataSource"/> from DI.
    /// </summary>
    public static IServiceCollection AddPostgresSseBackplane(
        this IServiceCollection services,
        TimeSpan? connectionTtl = null)
    {
        return RegisterCoreServices(services, connectionTtl);
    }

    /// <summary>
    /// Adds the Postgres EF realtime interceptor to the DbContext options.
    /// Use instead of <c>AddEfRealtimeInterceptor</c> when the Postgres backplane is active.
    /// </summary>
    public static DbContextOptionsBuilder AddPostgresEfRealtimeInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<PostgresEfRealtimeInterceptor>();
        return builder.AddInterceptors(interceptor);
    }

    private static IServiceCollection RegisterCoreServices(IServiceCollection services, TimeSpan? connectionTtl)
    {
        services.AddSingleton(sp =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            var logger = sp.GetRequiredService<ILogger<PostgresBackplane>>();
            var jsonOptions = sp.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
            return new PostgresBackplane(dataSource, logger, jsonOptions);
        });

        services.AddSingleton<ISseBackplane>(sp => sp.GetRequiredService<PostgresBackplane>());

        services.AddSingleton<PostgresEfRealtimeInterceptor>();

        services.AddSingleton<IHostedService>(sp =>
        {
            var backplane = sp.GetRequiredService<PostgresBackplane>();
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<PostgresBackplaneHostedService>>();
            var realtimeManager = sp.GetService<RealtimeManager>();
            return new PostgresBackplaneHostedService(backplane, dataSource, scopeFactory, logger, connectionTtl, realtimeManager);
        });

        return services;
    }
}
