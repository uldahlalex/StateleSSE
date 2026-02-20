using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace StateleSSE.AspNetCore.EfRealtime;

/// <summary>
/// Manages realtime subscriptions that trigger queries on SaveChanges and deliver results directly to the subscriber.
/// </summary>
public interface IRealtimeManager
{
    /// <summary>
    /// Register a realtime subscription. When SaveChanges is called on a <typeparamref name="TDbContext"/>
    /// and <paramref name="criteria"/> matches the changes, <paramref name="query"/> is executed
    /// and the result is passed to <paramref name="deliver"/>.
    /// Returns a subscription ID to pass to <see cref="Unsubscribe"/> when done.
    /// </summary>
    string Subscribe<TDbContext>(
        Func<List<EntityEntry>, bool> criteria,
        Func<TDbContext, Task<object?>> query,
        Func<object?, Task> deliver) where TDbContext : DbContext;

    /// <summary>
    /// Remove a subscription by the ID returned from <see cref="Subscribe{TDbContext}"/>.
    /// </summary>
    void Unsubscribe(string subscriptionId);
}
