namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

internal sealed class SseConnectionGroup
{
    public string ConnectionId { get; set; } = default!;
    public string GroupName { get; set; } = default!;
}
