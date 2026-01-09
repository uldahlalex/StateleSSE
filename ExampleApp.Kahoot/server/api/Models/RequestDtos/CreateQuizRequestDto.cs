using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record CreateQuizRequestDto
{
    [Required] public required string QuizName { get; init; }

    [MinLength(1)] public required List<CreateQuizDtoQuestions> Questions { get; init; }
}