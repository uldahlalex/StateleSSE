namespace StateleSSE.AspNetCore;

/// <summary>
/// Tracks membership of an SSE connection in a named presence group. Include via <see cref="DbContextModelBuilderExtensionsForPresence.ConfigureModel"/>.
/// </summary>
public class SseConnectionMetadataGroup
{
    public string ConnectionId { get; set; } = default!;
    public string GroupName { get; set; } = default!;
    public SseConnectionMetadata ConnectionMetadata { get; set; } = default!;
}
