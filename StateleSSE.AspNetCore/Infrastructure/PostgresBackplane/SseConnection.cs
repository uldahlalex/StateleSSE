namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

internal sealed class SseConnection
{
    public string ConnectionId { get; set; } = default!;
    public string ServerId { get; set; } = default!;
    public DateTimeOffset LastSeen { get; set; }
}
