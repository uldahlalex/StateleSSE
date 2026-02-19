using Microsoft.EntityFrameworkCore;

namespace server;

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)                                                                                           
    {                                                                                                                                                            
        modelBuilder.HasDefaultSchema("chat");                                                                                          
    }      

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRoom> UserRooms { get; set; }
}