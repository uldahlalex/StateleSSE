namespace api.Models.ReturnDtos;

public class ActiveUserReturnDto
{
    public string UserId { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string ConnectionId { get; set; } = null!;
    public DateTime ConnectedAt { get; set; }
}
