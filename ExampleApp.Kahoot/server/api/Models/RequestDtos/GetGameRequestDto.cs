using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record GetGameRequestDto
{
    [Required] public required string GameId { get; init; }
}