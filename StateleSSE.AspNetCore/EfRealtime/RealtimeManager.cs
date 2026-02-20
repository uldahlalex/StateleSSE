using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeManager : IRealtimeManager
{
    private readonly ConcurrentDictionary<string, RealtimeSubscription> _subscriptions = new();

    public string Subscribe<TDbContext>(
        Func<ChangeSnapshot, bool> criteria,
        Func<TDbContext, Task<object?>> query,
        Func<object?, Task> deliver) where TDbContext : DbContext
    {
        var id = Guid.NewGuid().ToString();
        _subscriptions[id] = new RealtimeSubscription
        {
            SubscriptionId = id,
            DbContextType = typeof(TDbContext),
            Criteria = criteria,
            Query = ctx => query((TDbContext)ctx),
            Deliver = deliver
        };
        return id;
    }

    public void Unsubscribe(string subscriptionId) => _subscriptions.TryRemove(subscriptionId, out _);

    internal IReadOnlyList<RealtimeSubscription> GetMatchingSubscriptions(ChangeSnapshot snapshot, Type? dbContextType = null) =>
        _subscriptions.Values
            .Where(s => (dbContextType is null || s.DbContextType.IsAssignableFrom(dbContextType)) && s.Criteria(snapshot))
            .ToList();
}
