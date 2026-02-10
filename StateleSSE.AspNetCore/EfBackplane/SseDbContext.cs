using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore.EfBackplane;

public abstract class SseDbContext : DbContext
{
    protected SseDbContext(DbContextOptions options) : base(options) { }

    public DbSet<SseConnection> SseConnections => Set<SseConnection>();
    public DbSet<SseGroupMember> SseGroupMembers => Set<SseGroupMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SseConnection>(e =>
        {
            e.HasKey(c => c.ConnectionId);
        });

        builder.Entity<SseGroupMember>(e =>
        {
            e.HasKey(gm => new { gm.ConnectionId, gm.GroupName });
            e.HasIndex(gm => gm.GroupName);
        });
    }
}
