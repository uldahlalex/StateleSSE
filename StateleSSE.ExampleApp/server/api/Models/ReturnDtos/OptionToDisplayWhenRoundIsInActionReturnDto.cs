using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Option DTO without circular references
///     Excludes: Question (parent), Answers collection
/// </summary>
[TranspilationSource]
public record OptionToDisplayWhenRoundIsInActionReturnDto
{
    public OptionToDisplayWhenRoundIsInActionReturnDto()
    {
    }

    public OptionToDisplayWhenRoundIsInActionReturnDto(Option entity)
    {
        Id = entity.Id;
        Description = entity.Description;
        Questionid = entity.Questionid;
    }

    public string Id { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Questionid { get; init; } = null!;
}