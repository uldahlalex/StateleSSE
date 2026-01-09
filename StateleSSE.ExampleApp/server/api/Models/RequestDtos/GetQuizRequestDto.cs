using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record GetQuizRequestDto
{
    [Required] public required string QuizId { get; init; }
}