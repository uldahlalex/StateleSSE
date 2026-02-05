using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly RealtimeManager _manager;
    private readonly ISseBackplane _backplane;

    // Keyed by DbContext instance ID so concurrent saves on different contexts don't collide.
    private readonly ConcurrentDictionary<Guid, List<RealtimeSubscription>> _pending = new();

    public RealtimeSaveChangesInterceptor(RealtimeManager manager, ISseBackplane backplane)
    {
        _manager = manager;
        _backplane = backplane;
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
        var matched = _manager.GetSubscriptionsForContext(context.GetType())
            .Where(s => s.Criteria(snapshot))
            .ToList();

        if (matched.Count > 0)
            _pending[context.ContextId.InstanceId] = matched;
    }

    private async Task BroadcastAsync(DbContext context)
    {
        if (!_pending.TryRemove(context.ContextId.InstanceId, out var matched))
            return;

        foreach (var sub in matched)
        {
            var data = await sub.Query(context);
            if (data is not null)
                await _backplane.Clients.SendToGroupAsync(sub.GroupName, data);
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
