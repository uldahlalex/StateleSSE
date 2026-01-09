using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record SendMessageToQuizRequestDto
{
    [Required] public required string GameId { get; init; }

    [Required] public required string Message { get; init; }
}