using Microsoft.EntityFrameworkCore;

namespace server;

[PrimaryKey(nameof(Id))]
public class Message
{
    public string Id { get; set; }
    public string Content { get; set; }

    public string UserId { get; set; }
    public User User { get; set; }

    public string RoomId { get; set; }
    public Room Room { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}