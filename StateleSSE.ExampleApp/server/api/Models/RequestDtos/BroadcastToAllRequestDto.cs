using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record BroadcastToAllRequestDto
{
    [Required] public required string Message { get; init; }
}