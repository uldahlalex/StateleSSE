using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Question DTO without circular references
///     Includes: Options collection
///     Excludes: Quiz (parent), Gamerounds collection
/// </summary>
[TranspilationSource]
public record QuestionReturnDto
{
    public QuestionReturnDto()
    {
    }

    public QuestionReturnDto(Question entity)
    {
        Id = entity.Id;
        Description = entity.Description;
        Quizid = entity.Quizid;
        Seconds = entity.Seconds;
        Options = entity.Options?.Select(o => new OptionToDisplayWhenRoundIsInActionReturnDto(o)).ToList() ?? new List<OptionToDisplayWhenRoundIsInActionReturnDto>();
    }

    public string Id { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Quizid { get; init; } = null!;
    public int Seconds { get; init; }
    public List<OptionToDisplayWhenRoundIsInActionReturnDto> Options { get; init; } = new();
}