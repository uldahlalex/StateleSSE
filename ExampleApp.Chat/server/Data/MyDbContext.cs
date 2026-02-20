using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

namespace server;

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat");
        SsePresence.ConfigureModel(modelBuilder);
        modelBuilder.Entity<SseConnection>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRoom> UserRooms { get; set; }
    public DbSet<Poke> Pokes { get; set; }
    public DbSet<SseConnection> SseConnections => Set<SseConnection>();
    public DbSet<SseConnectionGroup> SseConnectionGroups => Set<SseConnectionGroup>();
}
