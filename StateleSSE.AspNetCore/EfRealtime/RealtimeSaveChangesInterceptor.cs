using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly RealtimeManager _manager;
    private readonly ISseBackplane _backplane;
    private readonly ILogger<RealtimeSaveChangesInterceptor> _logger;

    // Keyed by DbContext instance ID so concurrent saves on different contexts don't collide.
    private readonly ConcurrentDictionary<Guid, List<RealtimeSubscription>> _pending = new();

    public RealtimeSaveChangesInterceptor(RealtimeManager manager, ISseBackplane backplane, ILogger<RealtimeSaveChangesInterceptor> logger)
    {
        _manager = manager;
        _backplane = backplane;
        _logger = logger;
    }

    // --- Before save: snapshot change tracker state and match against criteria ---

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            CaptureMatchingSubscriptions(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            CaptureMatchingSubscriptions(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // --- After save: execute queries for matched subscriptions and broadcast ---

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        if (eventData.Context is not null)
            BroadcastAsync(eventData.Context).GetAwaiter().GetResult();

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await BroadcastAsync(eventData.Context);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    // --- On failure: clean up pending notifications ---

    public override void SaveChangesFailed(
        DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
            _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // --- Private helpers ---

    private void CaptureMatchingSubscriptions(DbContext context)
    {
        var snapshot = CreateSnapshot(context);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var entry in snapshot.Entries)
                _logger.LogDebug("EfRealtime: Change detected - {EntityType} ({State})", entry.Entity.GetType().Name, entry.State);
        }

        var subscriptions = _manager.GetSubscriptionsForContext(context.GetType()).ToList();
        _logger.LogDebug("EfRealtime: Evaluating {Count} subscriptions for {ContextType}", subscriptions.Count, context.GetType().Name);

        var matched = subscriptions.Where(s =>
        {
            var result = s.Criteria(snapshot);
            _logger.LogDebug("EfRealtime: Subscription '{Group}' criteria returned {Result}", s.GroupName, result);
            return result;
        }).ToList();

        if (matched.Count > 0)
            _pending[context.ContextId.InstanceId] = matched;

        _logger.LogDebug("EfRealtime: {MatchedCount} subscriptions matched", matched.Count);
    }

    private async Task BroadcastAsync(DbContext context)
    {
        if (!_pending.TryRemove(context.ContextId.InstanceId, out var matched))
        {
            _logger.LogDebug("EfRealtime: No pending subscriptions to broadcast for context {ContextId}", context.ContextId.InstanceId);
            return;
        }

        _logger.LogDebug("EfRealtime: Broadcasting to {Count} matched subscriptions", matched.Count);

        foreach (var sub in matched)
        {
            var data = await sub.Query(context);
            if (data is not null)
            {
                _logger.LogDebug("EfRealtime: Sending to group '{Group}'", sub.GroupName);
                await _backplane.Clients.SendToGroupAsync(sub.GroupName, data);
            }
            else
            {
                _logger.LogDebug("EfRealtime: Query for group '{Group}' returned null, skipping broadcast", sub.GroupName);
            }
        }
    }

    private static ChangeSnapshot CreateSnapshot(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not SseConnection and not SseConnectionGroup)
            .Select(e => new ChangeEntry(e.Entity, e.State))
            .ToList();

        return new ChangeSnapshot(entries);
    }
}
