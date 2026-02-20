namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

/// <summary>
/// Represents an active SSE connection tracked in Postgres.
/// </summary>
public class SseConnection
{
    public string ConnectionId { get; set; } = default!;
    public string ServerId { get; set; } = default!;
    public DateTimeOffset LastSeen { get; set; }

    public ICollection<SseConnectionGroup> Groups { get; set; } = [];
}
