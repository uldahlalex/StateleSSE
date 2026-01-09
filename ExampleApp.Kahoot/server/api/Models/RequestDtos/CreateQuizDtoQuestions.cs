using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record CreateQuizDtoQuestions
{
    [Required] public required string Question { get; init; }

    [MinLength(1)] public required List<CreateQuizAnswers> Answers { get; init; }
}