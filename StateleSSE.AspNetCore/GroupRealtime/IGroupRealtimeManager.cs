namespace StateleSSE.AspNetCore.GroupRealtime;

public interface IGroupRealtimeManager
{
    /// <summary>
    /// Subscribes the current connection to a group, receiving real-time updates when the group's state changes.
    /// </summary>
    /// <param name="groupName">The name of the group to subscribe to.</param>
    /// <param name="criteria">A filter predicate invoked on each group change event. Only changes where this returns true trigger a re-query.</param>
    /// <param name="query">A query executed against the backplane to produce the data pushed to the client.</param>
    void Subscribe(string groupName, Func<GroupChangedEventArgs, bool> criteria, Func<ISseBackplane, Task<object?>> query);

    /// <summary>
    /// Unsubscribes the current connection from a group, stopping real-time updates.
    /// </summary>
    /// <param name="groupName">The name of the group to unsubscribe from.</param>
    void Unsubscribe(string groupName);
}
