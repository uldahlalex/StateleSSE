using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record CreateQuizAnswers
{
    [Required] public required string Answer { get; init; }

    [Required] public required bool isCorrect { get; init; }
}