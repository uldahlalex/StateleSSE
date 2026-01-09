using api.Models.RequestDtos;
using api.Models.ReturnDtos;
using dataaccess;

namespace api.Services.Abstractions;

public interface IQuizService
{
    Task<QuizReturnDto> CreateQuiz(CreateQuizRequestDto dto, string requesterId);
    Task<List<QuizReturnDto>> ListQuizzes(string requesterId);
    Task<QuizReturnDto> GetQuiz(GetQuizRequestDto dto, string requesterId);

    /// <summary>
    ///     Essentially a "create answer" crud method
    /// </summary>
    Task<AnswerReturnDto> SubmitAnswer(CreateAnswerRequestDto dto, string requesterId);

    /// <summary>
    ///     When admin closes the next game round (or starts / ends a quiz depending on if there are any remaining questions)
    /// </summary>
    Task<GameroundReturnDto?> CreateNewRound(NextRoundRequestDto dto, string requesterId);

    Task<GameReturnDto> CreateGame(CreateGameFromQuizRequestDto dto, string requesterId);

    Task<GameReturnDto> GetGame(GetGameRequestDto dto);
    Task<List<GameReturnDto>> ListGames();
    Task<GameReturnDto> JoinGame(JoinGameRequestDto dto, string requesterId);

    /// <summary>
    ///     Admin uses this to finish the round manually
    /// </summary>
    Task<GameroundReturnDto> EndRound(EndRoundRequestDto dto, string requesterId);

    Task<UserReturnDto> GetUser(string? senderId);

    Task<Quiz> UpdateQuiz(string id);
    Task<GameroundReturnDto?> GetExistingRound(string dtoGameId);
    Task<RoundIsOverReturnDto?> GetRoundResults(string dtoGameId);
}