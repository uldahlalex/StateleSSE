using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record EndRoundRequestDto
{
    [Required] public required string RoundId { get; init; }
}