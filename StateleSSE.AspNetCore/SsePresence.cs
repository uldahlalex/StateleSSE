using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Helpers for adding SSE presence tracking to your DbContext.
/// </summary>
public static class SsePresence
{
    /// <summary>
    /// Registers <see cref="SseConnection"/> and <see cref="SseConnectionGroup"/> in your model.
    /// Call from <c>OnModelCreating</c>.
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
