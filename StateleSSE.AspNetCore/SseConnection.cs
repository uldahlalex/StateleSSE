namespace StateleSSE.AspNetCore;

/// <summary>
/// Tracks an active SSE connection. Include via <see cref="SsePresence.ConfigureModel"/> in your DbContext.
/// </summary>
public class SseConnection
{
    public string ConnectionId { get; set; } = default!;
    public string? OwnerId { get; set; }
    public ICollection<SseConnectionGroup> Groups { get; set; } = [];
}
