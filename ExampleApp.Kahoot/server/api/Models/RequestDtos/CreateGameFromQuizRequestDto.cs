using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record CreateGameFromQuizRequestDto
{
    [Required] public required string QuizId { get; init; }
}