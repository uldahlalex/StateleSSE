namespace StateleSSE.AspNetCore.EfBackplane;

public class SseConnection
{
    public string ConnectionId { get; set; } = null!;
    public string? UserId { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
}
