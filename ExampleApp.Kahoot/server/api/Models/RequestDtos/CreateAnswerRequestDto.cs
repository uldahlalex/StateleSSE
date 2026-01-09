using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record CreateAnswerRequestDto
{
    [Required] public required string OptionId { get; init; }

    [Required] public required string GameRoundId { get; init; }
}