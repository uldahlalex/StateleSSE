namespace StateleSSE.AspNetCore.GroupRealtime;

internal sealed class GroupRealtimeSubscription
{
    public required string GroupName { get; init; }
    public required Func<GroupChangedEventArgs, bool> Criteria { get; init; }
    public required Func<ISseBackplane, Task<object?>> Query { get; init; }
}
