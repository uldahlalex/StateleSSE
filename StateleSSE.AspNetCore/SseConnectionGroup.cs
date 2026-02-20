namespace StateleSSE.AspNetCore;

/// <summary>
/// Tracks membership of an SSE connection in a named presence group. Include via <see cref="SsePresence.ConfigureModel"/>.
/// </summary>
public class SseConnectionGroup
{
    public string ConnectionId { get; set; } = default!;
    public string GroupName { get; set; } = default!;
    public SseConnection Connection { get; set; } = default!;
}
