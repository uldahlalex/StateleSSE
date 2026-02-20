using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Standalone DbContext for SSE connection/group tracking tables.
/// Use this for migrations when you want a separate EF context, or call
/// <see cref="ConfigureModel"/> from your own DbContext's <c>OnModelCreating</c>
/// to include the SSE tables in your existing migrations.
/// </summary>
public class SseBackplaneDbContext : DbContext
{
    public SseBackplaneDbContext(DbContextOptions<SseBackplaneDbContext> options) : base(options) { }

    public DbSet<SseConnection> SseConnections => Set<SseConnection>();
    public DbSet<SseConnectionGroup> SseConnectionGroups => Set<SseConnectionGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureModel(modelBuilder);

    /// <summary>
    /// Configures the SSE connection and group tables including the FK relationship.
    /// Call this from your own DbContext's <c>OnModelCreating</c> to include the SSE tables
    /// in your existing migrations and enable LINQ queries over them.
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     SseBackplaneDbContext.ConfigureModel(modelBuilder);
    ///     // ... rest of your model
    /// }
    /// </code>
    /// </summary>
    public static void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SseConnection>(e => e.HasKey(x => x.ConnectionId));

        modelBuilder.Entity<SseConnectionGroup>(e =>
        {
            e.HasKey(x => new { x.ConnectionId, x.GroupName });
            e.HasOne(g => g.Connection)
             .WithMany(c => c.Groups)
             .HasForeignKey(g => g.ConnectionId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
