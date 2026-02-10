using Microsoft.EntityFrameworkCore;

namespace server;

[PrimaryKey(nameof(Id))]
public class Room
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string CreatedBy { get; set; }

    public List<Message> Messages { get; set; } = new();
    public List<UserRoom> UserRooms { get; set; } = new();
}