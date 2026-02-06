namespace StateleSSE.AspNetCore.GroupRealtime;

public interface IGroupRealtimeManager
{
    void Subscribe(string groupName, Func<GroupChangedEventArgs, bool> criteria, Func<IBackplaneGroups, Task<object?>> query);
    void Unsubscribe(string groupName);
}
