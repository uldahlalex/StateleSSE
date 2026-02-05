using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

/// <summary>
/// Manages realtime subscriptions that trigger queries on SaveChanges and broadcast results via the SSE backplane.
/// </summary>
public interface IRealtimeManager
{
    /// <summary>
    /// Register a realtime subscription for a backplane group. When <c>SaveChanges</c> is called on a
    /// <typeparamref name="TDbContext"/> and the <paramref name="criteria"/> matches the changes,
    /// the <paramref name="query"/> is executed and the result is broadcast to all clients in <paramref name="groupName"/>.
    /// If a subscription for this group already exists it is replaced.
    /// </summary>
    /// <param name="groupName">The backplane group to broadcast results to.</param>
    /// <param name="criteria">Predicate evaluated against a snapshot of changes captured before save. Return true to trigger the query.</param>
    /// <param name="query">Async query executed on the DbContext after save completes. The result is broadcast as JSON to the group.</param>
    void Subscribe<TDbContext>(
        string groupName,
        Func<ChangeSnapshot, bool> criteria,
        Func<TDbContext, Task<object?>> query) where TDbContext : DbContext;

    /// <summary>
    /// Remove a realtime subscription for a group.
    /// </summary>
    void Unsubscribe(string groupName);
}
