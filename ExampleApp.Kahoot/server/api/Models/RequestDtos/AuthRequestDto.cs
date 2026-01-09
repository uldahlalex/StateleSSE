using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record AuthRequestDto
{
    [Required] public required string Name { get; init; }

    [Required] public required string Password { get; init; }
}