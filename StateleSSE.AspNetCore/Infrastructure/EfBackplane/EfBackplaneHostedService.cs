using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.Infrastructure.EfBackplane;

internal sealed class EfBackplaneHostedService<TDbContext> : IHostedService, IAsyncDisposable
    where TDbContext : DbContext, ISseEfContext
{
    private readonly EfBackplane<TDbContext> _backplane;
    private readonly ILogger<EfBackplaneHostedService<TDbContext>> _logger;
    private readonly TimeSpan _connectionTtl;
    private readonly CancellationTokenSource _cts = new();
    private Task _heartbeatTask = Task.CompletedTask;

    public EfBackplaneHostedService(
        EfBackplane<TDbContext> backplane,
        ILogger<EfBackplaneHostedService<TDbContext>> logger,
        TimeSpan? connectionTtl = null)
    {
        _backplane = backplane;
        _logger = logger;
        _connectionTtl = connectionTtl ?? TimeSpan.FromSeconds(60);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _heartbeatTask = RunHeartbeatAsync(_cts.Token);
        _logger.LogInformation("EF backplane started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        await Task.WhenAny(_heartbeatTask, Task.Delay(5000, CancellationToken.None));
        _logger.LogInformation("EF backplane stopped");
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_connectionTtl.TotalSeconds / 3);
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await _backplane.UpdateHeartbeatAsync();
                await _backplane.DeleteStaleConnectionsAsync(_connectionTtl);
                _logger.LogTrace("Heartbeat: {Count} local connections", _backplane.GetLocalConnectionIds().Count());
            }
        }
        catch (OperationCanceledException) { }
    }

    public ValueTask DisposeAsync()
    {
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}
