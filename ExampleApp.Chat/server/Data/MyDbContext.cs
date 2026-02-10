using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore.EfBackplane;

namespace server;

public class MyDbContext : SseDbContext
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
