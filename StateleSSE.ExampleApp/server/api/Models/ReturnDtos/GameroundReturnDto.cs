using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Gameround DTO without circular references - MAIN DTO for broadcasting rounds
///     This is the primary DTO for NewRoundHasBegun
/// </summary>
[TranspilationSource]
public record GameroundReturnDto
{
    public GameroundReturnDto()
    {
    }

    public GameroundReturnDto(Gameround entity)
    {
        Id = entity.Id;
        Questionid = entity.Questionid;
        Gameid = entity.Gameid;
        Startedat = entity.Startedat;
        Endedat = entity.Endedat;
        Question = new QuestionReturnDto(entity.Question);
    }

    public string Id { get; init; } = null!;
    public string Questionid { get; init; } = null!;
    public string Gameid { get; init; } = null!;
    public DateTime Startedat { get; init; }
    public DateTime? Endedat { get; init; }

    /// <summary>
    ///     The question being asked in this round (with all answer options)
    /// </summary>
    public QuestionReturnDto? Question { get; init; }
    
}