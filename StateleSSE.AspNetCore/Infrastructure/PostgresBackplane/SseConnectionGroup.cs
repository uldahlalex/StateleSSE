namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

/// <summary>
/// Represents membership of an SSE connection in a named group.
/// </summary>
public class SseConnectionGroup
{
    public string ConnectionId { get; set; } = default!;
    public string GroupName { get; set; } = default!;

    public SseConnection Connection { get; set; } = default!;
}
