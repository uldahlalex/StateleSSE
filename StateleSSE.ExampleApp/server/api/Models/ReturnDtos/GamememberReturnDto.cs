using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Gamemember DTO without circular references
///     Includes: User
///     Excludes: Game (parent)
/// </summary>
[TranspilationSource]
public record GamememberReturnDto
{
    public GamememberReturnDto()
    {
    }

    public GamememberReturnDto(Gamemember entity)
    {
        Userid = entity.Userid;
        Gameid = entity.Gameid;
        Joinedat = entity.Joinedat;
        User = entity.User != null ? new UserReturnDto(entity.User) : null;
    }

    public string Userid { get; init; } = null!;
    public string Gameid { get; init; } = null!;
    public DateTime Joinedat { get; init; }
    public UserReturnDto? User { get; init; }
}