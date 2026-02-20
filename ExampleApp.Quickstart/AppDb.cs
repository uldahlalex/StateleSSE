using Microsoft.EntityFrameworkCore;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SseConnection> Connections => Set<SseConnection>()
}

public class Message
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
