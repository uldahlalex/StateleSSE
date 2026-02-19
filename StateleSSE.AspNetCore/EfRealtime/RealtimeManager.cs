using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeManager : IRealtimeManager
{
    private readonly ISseBackplane _backplane;
    private readonly ConcurrentDictionary<string, RealtimeSubscription> _subscriptions = new();
    private readonly object _lock = new();

    public RealtimeManager(ISseBackplane backplane)
    {
        _backplane = backplane;
        backplane.OnClientDisconnected += (_, e) => UnsubscribeAll(e.ConnectionId);
    }

    public async Task SubscribeAsync<TDbContext>(
        string connectionId,
        string groupName,
        Func<ChangeSnapshot, bool> criteria,
        Func<TDbContext, Task<object?>> query) where TDbContext : DbContext
    {
        await _backplane.Groups.AddToGroupAsync(connectionId, groupName);

        lock (_lock)
        {
            var sub = _subscriptions.GetOrAdd(groupName, _ => new RealtimeSubscription
            {
                GroupName = groupName,
                DbContextType = typeof(TDbContext),
                Criteria = criteria,
                Query = ctx => query((TDbContext)ctx)
            });
            sub.ConnectionIds.Add(connectionId);
        }
    }

    public async Task UnsubscribeAsync(string connectionId, string groupName)
    {
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(groupName, out var sub))
                return;
            sub.ConnectionIds.Remove(connectionId);
            if (sub.ConnectionIds.Count == 0)
                _subscriptions.TryRemove(groupName, out _);
        }

        await _backplane.Groups.RemoveFromGroupAsync(connectionId, groupName);
    }

    public void UnsubscribeAll(string connectionId)
    {
        lock (_lock)
        {
            var groups = _subscriptions.Values
                .Where(s => s.ConnectionIds.Contains(connectionId))
                .Select(s => s.GroupName)
                .ToList();
            foreach (var group in groups)
            {
                if (!_subscriptions.TryGetValue(group, out var sub))
                    continue;
                sub.ConnectionIds.Remove(connectionId);
                if (sub.ConnectionIds.Count == 0)
                    _subscriptions.TryRemove(group, out _);
            }
        }
        // Backplane group cleanup is handled by DisconnectAsync (cascade in Postgres,
        // explicit removal in Redis/InMemory).
    }

    internal IReadOnlyList<RealtimeSubscription> GetSubscriptionsForContext(Type dbContextType)
    {
        return _subscriptions.Values
            .Where(s => s.DbContextType.IsAssignableFrom(dbContextType))
            .ToList();
    }

    internal IReadOnlyList<RealtimeSubscription> GetMatchingSubscriptions(ChangeSnapshot snapshot)
    {
        lock (_lock)
        {
            return _subscriptions.Values.Where(s => s.Criteria(snapshot)).ToList();
        }
    }
}
