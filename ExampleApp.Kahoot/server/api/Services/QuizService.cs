using System.ComponentModel.DataAnnotations;
using api.Models.RequestDtos;
using api.Models.ReturnDtos;
using api.Repositories.Abstractions;
using api.Services.Abstractions;
using dataaccess;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class QuizService(TimeProvider timeProvider, IQuizRepository repository) : IQuizService
{
    public async Task<QuizReturnDto> CreateQuiz(CreateQuizRequestDto dto, string userId)
    {
        var user = await repository.UserQuery().FirstOrDefaultAsync(u => u.Id == userId)
                   ?? throw new HubException("Could not find user with ID: " + userId);

        var quiz = new Quiz(dto.QuizName, user);

        var question = new Question("What is 2+2?", 30, quiz);
        var option1 = new Option("4", true, question);
        var option2 = new Option("5", false, question);

        repository.Add(quiz);
        repository.Add(question);
        repository.Add(option1);
        repository.Add(option2);

        await repository.SaveChangesAsync();

        return await repository.QuizQuery()
            .Where(q => q.Id == quiz.Id)
            .Select(q => new QuizReturnDto(q))
            .FirstAsync();
    }

    public async Task<List<QuizReturnDto>> ListQuizzes(string userId)
    {
        return await repository.QuizQuery()
            .Select(q => new QuizReturnDto(q))
            .ToListAsync();
    }

    public async Task<QuizReturnDto> GetQuiz(GetQuizRequestDto dto, string userId)
    {
        return await repository.QuizQuery()
            .Where(q => q.Id == dto.QuizId)
            .Select(q => new QuizReturnDto(q))
            .FirstOrDefaultAsync() ?? throw new ValidationException("Not found");
    }


    public async Task<AnswerReturnDto> SubmitAnswer(CreateAnswerRequestDto dto, string requesterId)
    {
        var existingAnswer = await repository.AnswerQuery()
            .Where(a => a.Gameround == dto.GameRoundId && a.Userid == requesterId)
            .Select(a => new AnswerReturnDto(a))
            .FirstOrDefaultAsync();

        if (existingAnswer != null)
        {
            throw new HubException("You have already answered this round!");
        }

        var optionExists = await repository.OptionQuery()
                         .AnyAsync(o => o.Id == dto.OptionId);
        if (!optionExists)
        {
            throw new HubException("Could not find option with ID: " + dto.OptionId);
        }

        var gameround = await repository.GameroundQuery()
                            .FirstOrDefaultAsync(g => g.Id == dto.GameRoundId) ??
                        throw new HubException("Could not find game round with ID " + dto.GameRoundId);

        if (gameround.Endedat != null)
        {
            throw new HubException("This round has already ended!");
        }

        var answer = new Answer(requesterId, dto.GameRoundId, dto.OptionId, timeProvider.GetUtcNow().UtcDateTime);
        repository.Add(answer);
        await repository.SaveChangesAsync();

        return await repository.AnswerQuery()
            .Where(a => a.Gameround == dto.GameRoundId && a.Userid == requesterId)
            .Select(a => new AnswerReturnDto(a))
            .FirstAsync();
    }

    /// <summary>
    /// The actual method that "starts" a new round by creating it
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="requesterId"></param>
    /// <returns></returns>
    /// <exception cref="HubException"></exception>
    public async Task<GameroundReturnDto?> CreateNewRound(NextRoundRequestDto dto, string requesterId)
    {
        var game = await repository.GameQuery()
                       .FirstOrDefaultAsync(g => g.Id == dto.GameId)
                   ?? throw new HubException("Could not find game with ID: " + dto.GameId);

        if (game.Hostid != requesterId)
            throw new HubException("Cannot start next round unless you host the quiz!");

        var askedQuestionIds = await repository.GameroundQuery()
            .Where(gr => gr.Gameid == game.Id)
            .Select(gr => gr.Questionid)
            .ToHashSetAsync();

        var remainingQuestions = game.Quiz.Questions
            .Where(q => !askedQuestionIds.Contains(q.Id))
            .ToList();

        if (remainingQuestions.Count == 0)
            return null;

        var gameround = new Gameround(remainingQuestions.First(), game, timeProvider.GetUtcNow().UtcDateTime);
        repository.Add(gameround);
        await repository.SaveChangesAsync();

        return await repository.GameroundQuery()
            .Where(gr => gr.Id == gameround.Id)
            .Select(gr => new GameroundReturnDto(gr))
            .FirstAsync();
    }

    public async Task<GameReturnDto> CreateGame(CreateGameFromQuizRequestDto dto, string userId)
    {
        var quiz = await repository.QuizQuery()
                       .FirstOrDefaultAsync(q => q.Id == dto.QuizId)
                   ?? throw new HubException("Could not find quiz with ID: " + dto.QuizId);
        var user = await repository.UserQuery()
                       .FirstOrDefaultAsync(u => u.Id == userId)
                   ?? throw new HubException("Could not find user with ID: " + userId);

        var game = new Game(user, quiz);
        repository.Add(game);
        await repository.SaveChangesAsync();

        return await repository.GameQuery()
            .Where(g => g.Id == game.Id)
            .Select(g => new GameReturnDto(g))
            .FirstAsync();
    }

    public async Task<GameReturnDto> GetGame(GetGameRequestDto dto)
    {
        return await repository.GameQuery()
                   .Where(g => g.Id == dto.GameId)
                   .Select(g => new GameReturnDto(g))
                   .FirstOrDefaultAsync()
               ?? throw new HubException("Could not find game with ID: " + dto.GameId);
    }

    public async Task<List<GameReturnDto>> ListGames()
    {
        return await repository.GameQuery()
            .Select(g => new GameReturnDto(g))
            .ToListAsync();
    }

    public async Task<GameReturnDto> JoinGame(JoinGameRequestDto dto, string requesterId)
    {
        var game = await repository.GameQuery()
                       .FirstOrDefaultAsync(g => g.Id == dto.GameId) ??
                   throw new ValidationException("Could not find game with ID: " + dto.GameId);
        var player = await repository.UserQuery()
                         .FirstOrDefaultAsync(u => u.Id == requesterId) ??
                     throw new ValidationException("Could not find user with ID: " + requesterId);

        if (game.Gamerounds.Count > 0 && game.Gamerounds.Count(gr => gr.Endedat == null) == 0)
            throw new ValidationException("Game has ended!");

        var alreadyExists = await repository.GamememberQuery()
            .AnyAsync(g => g.Gameid == dto.GameId && g.Userid == requesterId);

        if (!alreadyExists)
        {
            repository.Add(new Gamemember(player, game, timeProvider.GetUtcNow().UtcDateTime));
            await repository.SaveChangesAsync();
        }

        return await repository.GameQuery()
            .Where(g => g.Id == dto.GameId)
            .Select(g => new GameReturnDto(g))
            .FirstAsync();
    }

    public async Task<GameroundReturnDto> EndRound(EndRoundRequestDto dto, string requesterId)
    {
        var gameRound = await repository.GameroundQuery()
                            .AsTracking()
                            .FirstOrDefaultAsync(g => g.Id == dto.RoundId) ??
                        throw new ValidationException("Could not find round with ID: " + dto.RoundId);

        if (gameRound.Endedat != null)
            throw new ValidationException("Game is already ended!");
        gameRound.Endedat = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync();

        return await repository.GameroundQuery()
            .Where(gr => gr.Id == dto.RoundId)
            .Select(gr => new GameroundReturnDto(gr))
            .FirstAsync();
    }

    public async Task<UserReturnDto> GetUser(string? senderId)
    {
        return await repository.UserQuery()
            .Where(u => u.Id == senderId)
            .Select(u => new UserReturnDto(u))
            .FirstOrDefaultAsync() ?? throw new ValidationException("User not found");
    }

    public async Task<GameroundReturnDto?> GetRound(string roundId)
    {
        return await repository.GameroundQuery()
            .Where(gr => gr.Id == roundId)
            .Select(gr => new GameroundReturnDto(gr))
            .FirstOrDefaultAsync();
    }

    public async Task<Quiz> UpdateQuiz(string id)
    {
        return await repository.UpdateQuiz(id);
    }

    public async Task<GameroundReturnDto?> GetExistingRound(string dtoGameId)
    {
        return await repository.GameroundQuery()
            .Where(gr => gr.Gameid == dtoGameId && gr.Endedat == null)
            .Select(gr => new GameroundReturnDto(gr))
            .FirstOrDefaultAsync();
    }

    public async Task<RoundIsOverReturnDto?> GetRoundResults(string dtoGameId)
    {
        return await repository.GameroundQuery()
            .Where(gr => gr.Gameid == dtoGameId && gr.Endedat != null)
            .OrderBy(gr => gr.Endedat)
            .Select(gr => new RoundIsOverReturnDto(gr))
            .FirstOrDefaultAsync();
    }
}