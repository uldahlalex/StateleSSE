using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.Infrastructure.PostgresBackplane;

/// <summary>
/// Standalone DbContext for SSE connection/group tracking tables.
/// Alternatively call <see cref="ConfigureModel"/> from your own DbContext's OnModelCreating.
/// </summary>
public class SseBackplaneDbContext : DbContext
{
    public SseBackplaneDbContext(DbContextOptions<SseBackplaneDbContext> options) : base(options) { }

    internal DbSet<SseConnection> SseConnections => Set<SseConnection>();
    internal DbSet<SseConnectionGroup> SseConnectionGroups => Set<SseConnectionGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureModel(modelBuilder);

    /// <summary>
    /// Configure the SSE backplane tables. Call this from your own DbContext's OnModelCreating
    /// when using the integrated mode.
    /// </summary>
    public static void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SseConnection>(e => e.HasKey(x => x.ConnectionId));
        modelBuilder.Entity<SseConnectionGroup>(e => e.HasKey(x => new { x.ConnectionId, x.GroupName }));
    }
}
