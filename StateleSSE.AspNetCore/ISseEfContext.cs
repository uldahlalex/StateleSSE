using Microsoft.EntityFrameworkCore;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Implement this interface on your DbContext to enable <see cref="EfServiceCollectionExtensions.AddEfSseBackplane{TDbContext}"/>.
/// Call <see cref="SseBackplaneDbContext.ConfigureModel"/> from your <c>OnModelCreating</c> to configure the tables.
/// </summary>
public interface ISseEfContext
{
    DbSet<SseConnection> SseConnections { get; }
    DbSet<SseConnectionGroup> SseConnectionGroups { get; }
}
