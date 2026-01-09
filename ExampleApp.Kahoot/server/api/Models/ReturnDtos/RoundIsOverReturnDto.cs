using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

[TranspilationSource]
public record RoundIsOverReturnDto
{
    public RoundIsOverReturnDto()
    {
    }

    public RoundIsOverReturnDto(Gameround entity)
    {
        Id = entity.Id;
        Questionid = entity.Questionid;
        Gameid = entity.Gameid;
        Startedat = entity.Startedat;
        Endedat = entity.Endedat;
        Answers = entity.Answers.Select(a => new UserScoreReturnDto(a)).ToList();

    }

    public string Id { get; init; } = null!;
    public string Questionid { get; init; } = null!;
    public string Gameid { get; init; } = null!;
    public DateTime Startedat { get; init; }
    public DateTime? Endedat { get; init; }
    public List<UserScoreReturnDto> Answers { get; set; }
    
}