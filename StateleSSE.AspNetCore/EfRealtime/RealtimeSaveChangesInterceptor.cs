using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly RealtimeManager _manager;
    private readonly ILogger<RealtimeSaveChangesInterceptor> _logger;

    private readonly ConcurrentDictionary<Guid, List<RealtimeSubscription>> _pending = new();

    public RealtimeSaveChangesInterceptor(RealtimeManager manager, ILogger<RealtimeSaveChangesInterceptor> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) CaptureMatchingSubscriptions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) CaptureMatchingSubscriptions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null) DeliverAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) await DeliverAsync(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null) _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureMatchingSubscriptions(DbContext context)
    {
        var snapshot = CreateSnapshot(context);
        var matched = _manager.GetMatchingSubscriptions(snapshot, dbContextType: context.GetType()).ToList();
        _logger.LogDebug("EfRealtime: {MatchedCount} subscriptions matched for {ContextType}", matched.Count, context.GetType().Name);
        if (matched.Count > 0) _pending[context.ContextId.InstanceId] = matched;
    }

    private async Task DeliverAsync(DbContext context)
    {
        if (!_pending.TryRemove(context.ContextId.InstanceId, out var matched)) return;
        foreach (var sub in matched)
        {
            var data = await sub.Query(context);
            if (data is not null) await sub.Deliver(data);
        }
    }

    private static ChangeSnapshot CreateSnapshot(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new ChangeEntry(e.Entity, e.State))
            .ToList();
        return new ChangeSnapshot(entries);
    }
}
