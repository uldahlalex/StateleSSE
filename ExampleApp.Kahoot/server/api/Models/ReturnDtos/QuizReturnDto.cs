using dataaccess;
using Tapper;

namespace api.Models.ReturnDtos;

/// <summary>
///     Quiz DTO without circular references
///     Includes: Questions (with Options), CreatedBy user
///     Excludes: Games collection
/// </summary>
[TranspilationSource]
public record QuizReturnDto
{
    private  QuizReturnDto()
    {
    }

    public QuizReturnDto(Quiz entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Createdby = entity.Createdby;
        CreatedbyNavigation = entity.CreatedbyNavigation != null ? new UserReturnDto(entity.CreatedbyNavigation) : null;
        // Questions = entity.Questions?.Select(q => new QuestionReturnDto(q)).ToList() ?? new List<QuestionReturnDto>();
        TotalQuestions = entity.Questions.Count;
    }

    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Createdby { get; init; } = null!;
    public UserReturnDto? CreatedbyNavigation { get; init; }
    public int TotalQuestions { get; init; }
    // public List<QuestionReturnDto> Questions { get; init; } = new();
}