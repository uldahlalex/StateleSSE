using Microsoft.EntityFrameworkCore;

namespace server;

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRoom> UserRooms { get; set; }
}

[PrimaryKey(nameof(Id))]
public class Room
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string CreatedBy { get; set; }

    public List<Message> Messages { get; set; } = new();
    public List<UserRoom> UserRooms { get; set; } = new();
}

[PrimaryKey(nameof(Id))]
public class Message
{
    public string Id { get; set; }
    public string Content { get; set; }

    public string UserId { get; set; }
    public User User { get; set; }

    public string RoomId { get; set; }
    public Room Room { get; set; }
    public DateTime CreatedAt { get; set; }
}

[PrimaryKey(nameof(Id))]
public class User
{
    public string Id { get; set; }
    public string Nickname { get; set; }

    public List<Message> Messages { get; set; } = new();
    public List<UserRoom> UserRooms { get; set; } = new();
}

[PrimaryKey(nameof(UserId), nameof(RoomId))]
public class UserRoom
{
    public string UserId { get; set; }
    public User User { get; set; }

    public string RoomId { get; set; }
    public Room Room { get; set; }
}
 
