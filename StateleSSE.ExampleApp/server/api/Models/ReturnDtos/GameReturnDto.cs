using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Game DTO without circular references
///     Includes: Host, Quiz (with Questions/Options), Gamemembers, Gamerounds
/// </summary>
[TranspilationSource]
public record GameReturnDto
{
    public GameReturnDto()
    {
    }

    public GameReturnDto(Game entity)
    {
        Id = entity.Id;
        Hostid = entity.Hostid;
        Quizid = entity.Quizid;
        Host = new UserReturnDto(entity.Host);
        Quiz = new QuizReturnDto(entity.Quiz);
        Gamemembers = entity.Gamemembers?.Select(gm => new GamememberReturnDto(gm)).ToList() ??
                      new List<GamememberReturnDto>();

    }

    public string Id { get; init; } = null!;
    public string Hostid { get; init; } = null!;
    public string Quizid { get; init; } = null!;
    public UserReturnDto? Host { get; init; }
    public QuizReturnDto? Quiz { get; init; }
    public List<GamememberReturnDto> Gamemembers { get; init; } = new();
}