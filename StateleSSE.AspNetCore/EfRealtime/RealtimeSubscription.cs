using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeSubscription
{
    public required string GroupName { get; init; }
    public required Type DbContextType { get; init; }
    public required Func<ChangeSnapshot, bool> Criteria { get; init; }
    public required Func<DbContext, Task<object?>> Query { get; init; }
    public HashSet<string> ConnectionIds { get; } = new();
}
