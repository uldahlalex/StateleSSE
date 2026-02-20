using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfRealtime;

internal sealed class RealtimeSubscription
{
    public required string SubscriptionId { get; init; }
    public required Type DbContextType { get; init; }
    public required Func<ChangeSnapshot, bool> Criteria { get; init; }
    public required Func<DbContext, Task<object?>> Query { get; init; }
    public required Func<object?, Task> Deliver { get; init; }
}
