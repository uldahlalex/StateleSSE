using Microsoft.EntityFrameworkCore;

namespace server;

[PrimaryKey(nameof(UserId), nameof(RoomId))]
public class UserRoom
{
    public string UserId { get; set; }
    public User User { get; set; }

    public string RoomId { get; set; }
    public Room Room { get; set; }
}