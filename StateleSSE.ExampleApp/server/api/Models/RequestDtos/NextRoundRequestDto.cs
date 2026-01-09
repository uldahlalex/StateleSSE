using Tapper;

namespace api.Models.RequestDtos;

[TranspilationSource]
public record NextRoundRequestDto
{
    public required string GameId { get; init; }
    public string? QuestionId { get; init; }
}