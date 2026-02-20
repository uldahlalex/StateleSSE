using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure.EfBackplane;

internal sealed class EfBackplaneHostedService<TDbContext> : IHostedService
    where TDbContext : DbContext, ISseEfContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfBackplaneHostedService<TDbContext>> _logger;

    public EfBackplaneHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EfBackplaneHostedService<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var stale = await ctx.SseConnections.ToListAsync(cancellationToken);
        if (stale.Count > 0)
        {
            ctx.SseConnections.RemoveRange(stale);
            await ctx.SaveChangesAsync(cancellationToken);
        }
        _logger.LogInformation("EF backplane started, cleared {Count} stale connections from previous run", stale.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EF backplane stopped");
        return Task.CompletedTask;
    }
}
