using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using StateleSSE.AspNetCore.EfRealtime;

namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

internal sealed class PostgresEfRealtimeInterceptor : SaveChangesInterceptor
{
    private readonly PostgresBackplane _backplane;
    private readonly ILogger<PostgresEfRealtimeInterceptor> _logger;
    private readonly ConcurrentDictionary<Guid, List<NotifyEntry>> _pending = new();

    public PostgresEfRealtimeInterceptor(PostgresBackplane backplane, ILogger<PostgresEfRealtimeInterceptor> logger)
    {
        _backplane = backplane;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            CaptureSnapshot(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            CaptureSnapshot(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
            BroadcastAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await BroadcastAsync(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
            _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureSnapshot(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new NotifyEntry(
                e.Entity.GetType().AssemblyQualifiedName ?? e.Entity.GetType().FullName ?? e.Entity.GetType().Name,
                e.Entity.GetType().FullName ?? e.Entity.GetType().Name,
                (int)e.State))
            .ToList();

        if (entries.Count > 0)
            _pending[context.ContextId.InstanceId] = entries;
    }

    private async Task BroadcastAsync(DbContext context)
    {
        if (!_pending.TryRemove(context.ContextId.InstanceId, out var entries))
            return;

        var payload = JsonSerializer.Serialize(new NotifyChanges(entries.ToArray()));
        await _backplane.NotifyAsync("sse:ef:changes", payload);
        _logger.LogDebug("EfRealtime: notified {Count} entity changes", entries.Count);
    }
}
