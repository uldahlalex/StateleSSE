using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Helpers for adding SSE presence tracking to your DbContext.
/// </summary>
public static class DbContextModelBuilderExtensionsForPresence
{
    /// <summary>
    /// Registers <see cref="SseConnectionMetadata"/> and <see cref="SseConnectionMetadataGroup"/> in your model.
    /// Call from <c>OnModelCreating</c>.
    /// </summary>
    public static void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SseConnectionMetadata>(e => e.HasKey(x => x.ConnectionId));

        modelBuilder.Entity<SseConnectionMetadataGroup>(e =>
        {
            e.HasKey(x => new { x.ConnectionId, x.GroupName });
            e.HasOne(g => g.ConnectionMetadata)
             .WithMany(c => c.Groups)
             .HasForeignKey(g => g.ConnectionId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
