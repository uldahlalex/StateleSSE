namespace StateleSSE.AspNetCore;

/// <summary>
/// Tracks an active SSE connection. Include via <see cref="DbContextModelBuilderExtensionsForPresence.ConfigureModel"/> in your DbContext.
/// </summary>
public class SseConnectionMetadata
{
    public string ConnectionId { get; set; } = default!;
    public string? OwnerId { get; set; }
    public ICollection<SseConnectionMetadataGroup> Groups { get; set; } = [];
}
