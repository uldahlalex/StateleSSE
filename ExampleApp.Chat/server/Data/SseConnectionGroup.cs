namespace server;

public class SseConnectionGroup
{
    public string ConnectionId { get; set; } = default!;
    public string GroupName { get; set; } = default!;

    public SseConnection Connection { get; set; } = default!;
}
