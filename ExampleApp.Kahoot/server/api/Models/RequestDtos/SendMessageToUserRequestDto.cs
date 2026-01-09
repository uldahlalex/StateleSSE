using System.ComponentModel.DataAnnotations;
using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record SendMessageToUserRequestDto
{
    [Required] public required string TargetUserId { get; init; }

    [Required] public required string Message { get; init; }
}