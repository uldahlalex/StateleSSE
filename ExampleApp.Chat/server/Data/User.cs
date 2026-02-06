using Microsoft.EntityFrameworkCore;

namespace server;

[PrimaryKey(nameof(Id))]
public class User
{
    public string Id { get; set; }
    public string Nickname { get; set; }
    public string Salt { get; set; }
    public string Hash { get; set; }

    public List<Message> Messages { get; set; } = new();
    public List<UserRoom> UserRooms { get; set; } = new();
}