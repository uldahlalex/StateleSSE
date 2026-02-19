using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

/// <summary>
/// Manages realtime subscriptions that trigger queries on SaveChanges and broadcast results via the SSE backplane.
/// </summary>
public interface IRealtimeManager
{
    /// <summary>
    /// Register a realtime subscription and add the connection to the backplane group.
    /// When <c>SaveChanges</c> is called on a <typeparamref name="TDbContext"/> and the
    /// <paramref name="criteria"/> matches the changes, the <paramref name="query"/> is executed
    /// and the result is broadcast to all clients in <paramref name="groupName"/>.
    /// Multiple connections can subscribe to the same group; the subscription stays alive
    /// until the last connection unsubscribes.
    /// </summary>
    Task SubscribeAsync<TDbContext>(
        string connectionId,
        string groupName,
        Func<ChangeSnapshot, bool> criteria,
        Func<TDbContext, Task<object?>> query) where TDbContext : DbContext;

    /// <summary>
    /// Remove a connection from a realtime subscription and from the backplane group.
    /// The subscription is removed when the last connection unsubscribes.
    /// </summary>
    Task UnsubscribeAsync(string connectionId, string groupName);

    /// <summary>
    /// Remove a connection from all realtime subscriptions. Called on client disconnect.
    /// Backplane group cleanup is handled by <c>DisconnectAsync</c> on the backplane itself.
    /// </summary>
    void UnsubscribeAll(string connectionId);
}
