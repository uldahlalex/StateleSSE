using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     User DTO without circular references
///     Excludes: Answers, Gamemembers, Games, Quizzes collections
/// </summary>
[TranspilationSource]
public record UserReturnDto
{
    public UserReturnDto()
    {
    }

    public UserReturnDto(User entity)
    {
        Id = entity.Id;
        Name = entity.Name;
    }

    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
}