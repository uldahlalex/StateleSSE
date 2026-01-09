using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Structured error response returned to SignalR clients
/// </summary>
[TranspilationSource]
public record ErrorResponse
{
    public required string Message { get; init; }
    public required string ErrorType { get; init; }
    public string? ErrorCode { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}