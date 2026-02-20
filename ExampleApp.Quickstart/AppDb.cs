using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        DbContextModelBuilderExtensionsForPresence.ConfigureModel(modelBuilder);
        modelBuilder.Entity<SseConnectionMetadata>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SseConnectionMetadata> Connections => Set<SseConnectionMetadata>();
    public DbSet<SseConnectionMetadataGroup> ConnectionGroups => Set<SseConnectionMetadataGroup>();
}

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
}

public class Message
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string RoomId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
