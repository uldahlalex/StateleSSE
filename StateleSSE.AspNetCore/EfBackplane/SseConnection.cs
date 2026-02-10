namespace StateleSSE.AspNetCore.EfBackplane;

public class SseConnection
{
    public string ConnectionId { get; set; } = null!;
    public DateTimeOffset ConnectedAt { get; set; }
}
