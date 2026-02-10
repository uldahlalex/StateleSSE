namespace StateleSSE.AspNetCore.EfBackplane;

public class SseGroupMember
{
    public string ConnectionId { get; set; } = null!;
    public string GroupName { get; set; } = null!;
}
