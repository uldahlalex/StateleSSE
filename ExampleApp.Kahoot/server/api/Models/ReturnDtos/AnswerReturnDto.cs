using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Answer DTO without circular references
///     Includes: User, selected Option
///     Excludes: GameroundNavigation (parent)
/// </summary>
[TranspilationSource]
public record AnswerReturnDto
{
    public AnswerReturnDto()
    {
    }

    public AnswerReturnDto(Answer entity)
    {
        Userid = entity.Userid;
        Gameround = entity.Gameround;
        Option = entity.Option;
        Answeredat = entity.Answeredat;
        User = entity.User != null ? new UserReturnDto(entity.User) : null;
        OptionNavigation = entity.OptionNavigation != null ? new OptionToDisplayWhenRoundIsInActionReturnDto(entity.OptionNavigation) : null;
    }

    public string Userid { get; init; } = null!;
    public string Gameround { get; init; } = null!;
    public string Option { get; init; } = null!;
    public DateTime? Answeredat { get; init; }
    public UserReturnDto? User { get; init; }
    public OptionToDisplayWhenRoundIsInActionReturnDto? OptionNavigation { get; init; }
}

[TranspilationSource]
public record UserScoreReturnDto
{
    public UserScoreReturnDto()
    {
    }

    public UserScoreReturnDto(Answer entity)
    {
        Userid = entity.Userid;
        Gameround = entity.Gameround;
        Option = entity.Option;
        IsCorrect = entity.OptionNavigation.Iscorrect;
        Answeredat = entity.Answeredat;
        User = new UserReturnDto(entity.User);
    }

    public string Userid { get; init; } = null!;
    public string Gameround { get; init; } = null!;
    public string Option { get; init; } = null!;
    public DateTime? Answeredat { get; init; }
    public bool IsCorrect { get; set; }
    public UserReturnDto? User { get; init; }
}