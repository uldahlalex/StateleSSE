using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeManager : IRealtimeManager
{
    private readonly ConcurrentDictionary<string, RealtimeSubscription> _subscriptions = new();

    public void Subscribe<TDbContext>(
        string groupName,
        Func<ChangeSnapshot, bool> criteria,
        Func<TDbContext, Task<object?>> query) where TDbContext : DbContext
    {
        _subscriptions[groupName] = new RealtimeSubscription
        {
            GroupName = groupName,
            DbContextType = typeof(TDbContext),
            Criteria = criteria,
            Query = ctx => query((TDbContext)ctx)
        };
    }

    public void Unsubscribe(string groupName)
    {
        _subscriptions.TryRemove(groupName, out _);
    }

    internal IReadOnlyList<RealtimeSubscription> GetSubscriptionsForContext(Type dbContextType)
    {
        return _subscriptions.Values
            .Where(s => s.DbContextType.IsAssignableFrom(dbContextType))
            .ToList();
    }
}
