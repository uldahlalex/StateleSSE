using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

namespace server;

public class MyDbContext : DbContext, ISseEfContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat");
        SseBackplaneDbContext.ConfigureModel(modelBuilder);
        modelBuilder.Entity<User>()
            .HasMany(u => u.Connections)
            .WithOne()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRoom> UserRooms { get; set; }
    public DbSet<SseConnection> SseConnections => Set<SseConnection>();
    public DbSet<SseConnectionGroup> SseConnectionGroups => Set<SseConnectionGroup>();
}
