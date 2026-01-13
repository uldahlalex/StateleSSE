using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using api.Models.RequestDtos;
using api.Models.ReturnDtos;
using api.Models.SseEventDtos;
using api.Repositories.Abstractions;
using api.Services.Abstractions;
using dataaccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

namespace api.Realtime;

// Example of using EventPublisher for DTO-centric architecture:
// await backplane.PublishEvent(gameId, new RoundStartedSubscribeRequestDto(...));
// This publishes to: game:{gameId}:RoundStartedSubscribeRequestDto

/// <summary>
/// PRODUCTION EXAMPLE: Kahoot game using SSE + REST + StateleSSE + EF Core
/// Demonstrates database persistence + realtime events with generic infrastructure
/// </summary>
public class KahootGameController(IQuizRepository repo, IAuthService authService, ISseBackplane backplane) : ControllerBase
{
    [HttpPost(nameof(Login))]
    public async Task<JwtResponse> Login([FromBody]AuthRequestDto dto)
    {
        return await authService.Login(dto);
    }
    
    [HttpPost(nameof(Register))]
    public async Task<JwtResponse> Register([FromBody]AuthRequestDto dto)
    {
        return await authService.Register(dto);
    }

    
    [HttpGet(nameof(GetQuizzes))]
    public async Task<List<QuizReturnDto>> GetQuizzes()
    {
        var quizzes = await repo.QuizQuery()
            .Select(q => new QuizReturnDto(q))
            .ToListAsync();
        return quizzes;
    }

    [HttpGet(nameof(GetQuiz))]
    public async Task<QuizReturnDto> GetQuiz([FromQuery] string quizId)
    {
        var quiz = await repo.QuizQuery()
            .Where(q => q.Id == quizId)
            .FirstOrDefaultAsync();

        if (quiz == null)
            throw new ValidationException("Quiz not found");

        return new QuizReturnDto(quiz);
    }

    [HttpGet(nameof(GameStream))]
    public async Task GameStream([FromQuery] string gameId)
    {
        // var channel = ChannelNamingExtensions.Channel("game", gameId);
        // await HttpContext.StreamSseWithInitialStateAsync(
        //     backplane,
        //     // channel,
        //     () => GetGameState(gameId),
        //     "game_state");
    }
   


    /// <summary>
    /// INTERNAL: This endpoint exists only to expose SSE event types to NSwag for TypeScript generation.
    /// It should never be called at runtime. Use GameStream endpoint for actual SSE subscriptions.
    /// </summary>
    [HttpGet("__internal/sse-event-types")]
    public GameEventUnion GetSseEventTypes()
    {
        throw new NotImplementedException("This endpoint is for type generation only");
    }

    [HttpPost(nameof(CreateGame))]
    public async Task<CreateGameResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        var quizInfo = await repo.QuizQuery()
            .Where(q => q.Id == request.QuizId)
            .Select(q => new { q.Id, q.Name, QuestionCount = q.Questions.Count })
            .FirstOrDefaultAsync();

        if (quizInfo == null)
            throw new ValidationException("Quiz not found");

        if (quizInfo.QuestionCount == 0)
            throw new ValidationException("Quiz has no questions");

        var hostExists = await repo.UserQuery()
            .AnyAsync(u => u.Id == request.HostUserId);
        if (!hostExists)
            throw new ValidationException("Host user not found");

        var game = new Game(request.HostUserId, request.QuizId);

        repo.Add(game);
        await repo.SaveChangesAsync();

        await backplane.PublishToGroup($"game:{game.Id}", new GameCreatedEvent(
            game.Id,
            quizInfo.Name,
            request.HostUserId,
            quizInfo.QuestionCount,
            DateTime.UtcNow
        ));

        return new CreateGameResponse(game.Id, quizInfo.Name, quizInfo.QuestionCount);
    }

    [HttpPost(nameof(JoinGame))]
    public async Task<JoinGameResponse> JoinGame([FromBody] JoinGameRequest request)
    {
        var gameExists = await repo.GameQuery()
            .AnyAsync(g => g.Id == request.GameId);
        if (!gameExists)
            throw new ValidationException("Game not found");

        var existingMember = await repo.GamememberQuery()
            .FirstOrDefaultAsync(gm => gm.Gameid == request.GameId && gm.Userid == request.UserId);

        if (existingMember != null)
            return new JoinGameResponse("already_joined", request.GameId, 0);

        var userName = await repo.UserQuery()
            .Where(u => u.Id == request.UserId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync();
        if (userName == null)
            throw new ValidationException("User not found");

        var gameMember = new Gamemember(request.UserId, request.GameId, DateTime.UtcNow);

        repo.Add(gameMember);
        await repo.SaveChangesAsync();

        var playerCount = await repo.GamememberQuery()
            .CountAsync(gm => gm.Gameid == request.GameId);

        await backplane.PublishToGroup($"game:{request.GameId}", new PlayerJoinedEvent(
            request.UserId,
            userName,
            playerCount,
            DateTime.UtcNow
        ));

        return new JoinGameResponse("joined", request.GameId, playerCount);
    }

    [HttpPost(nameof(StartRound))]
    public async Task<StartRoundResponse> StartRound([FromBody] StartRoundRequest request)
    {
        var gameExists = await repo.GameQuery()
            .AnyAsync(g => g.Id == request.GameId);
        if (!gameExists)
            throw new ValidationException("Game not found");

        var questionInfo = await repo.QuestionQuery()
            .Where(q => q.Id == request.QuestionId)
            .Select(q => new
            {
                q.Id,
                q.Description,
                q.Seconds,
                Options = q.Options.Select(o => new { o.Id, o.Description }).ToArray()
            })
            .FirstOrDefaultAsync();

        if (questionInfo == null)
            throw new ValidationException("Question not found");

        var gameround = new Gameround(request.QuestionId, request.GameId, DateTime.UtcNow);

        repo.Add(gameround);
        await repo.SaveChangesAsync();

        await backplane.PublishToGroup($"game:{request.GameId}", new RoundStartedEvent(
            gameround.Id,
            questionInfo.Id,
            questionInfo.Description,
            questionInfo.Seconds,
            questionInfo.Options.Select(o => new QuestionOptionInfo(o.Id, o.Description)).ToArray(),
            DateTime.UtcNow
        ));

        return new StartRoundResponse(gameround.Id, "round_started");
    }

    [HttpPost(nameof(SubmitAnswer))]
    public async Task<SubmitAnswerResponse> SubmitAnswer([FromBody] SubmitAnswerRequest request)
    {
        var gameround = await repo.GameroundQuery()
            .Where(gr => gr.Id == request.RoundId && gr.Endedat == null)
            .FirstOrDefaultAsync();

        if (gameround == null)
            throw new ValidationException("Round not found or already ended");

        var existingAnswer = await repo.AnswerQuery()
            .FirstOrDefaultAsync(a => a.Gameround == request.RoundId && a.Userid == request.UserId);

        if (existingAnswer != null)
            throw new ValidationException("Already answered this round");

        var option = gameround.Question.Options.FirstOrDefault(o => o.Id == request.OptionId);
        if (option == null)
            throw new ValidationException("Invalid option");

        var answer = new Answer(request.UserId, request.RoundId, request.OptionId, DateTime.UtcNow);

        repo.Add(answer);
        await repo.SaveChangesAsync();

        var answerCount = await repo.AnswerQuery()
            .CountAsync(a => a.Gameround == request.RoundId);

        await backplane.PublishToGroup($"game:{request.GameId}", new AnswerSubmittedEvent(
            request.UserId,
            answerCount,
            DateTime.UtcNow
        ));

        return new SubmitAnswerResponse("received", option.Iscorrect);
    }

    [HttpPost(nameof(EndRound))]
    public async Task<EndRoundResponse> EndRound([FromBody] EndRoundRequest request)
    {
        var gameround = await repo.GameroundQuery()
            .Where(gr => gr.Id == request.RoundId)
            .FirstOrDefaultAsync();

        if (gameround == null)
            throw new ValidationException("Round not found");

        gameround.Endedat = DateTime.UtcNow;
        await repo.SaveChangesAsync();

        var correctOptionId = gameround.Question.Options.First(o => o.Iscorrect).Id;
        var results = gameround.Answers.Select(a => new RoundResultEntry(
            a.Userid,
            a.User.Name,
            a.Option,
            a.Option == correctOptionId,
            a.Answeredat
        )).ToArray();

        var leaderboard = await CalculateLeaderboard(request.GameId);

        await backplane.PublishToGroup($"game:{request.GameId}", new RoundEndedEvent(
            request.RoundId,
            correctOptionId,
            results,
            leaderboard,
            DateTime.UtcNow
        ));

        return new EndRoundResponse("round_ended", results, leaderboard);
    }

    [HttpGet(nameof(GetGameState))]
    public async Task<GameStateResponse> GetGameState([FromQuery] string gameId)
    {
        var game = await repo.GameQuery()
            .Where(g => g.Id == gameId)
            .FirstOrDefaultAsync();

        if (game == null)
            throw new ValidationException("Game not found");

        var activeRound = game.Gamerounds.FirstOrDefault(gr => gr.Endedat == null);
        var leaderboard = await CalculateLeaderboard(gameId);

        var players = game.Gamemembers.Select(gm => new PlayerInfo(
            gm.Userid,
            gm.User.Name,
            gm.Joinedat
        )).ToArray();

        var currentRound = activeRound != null
            ? new CurrentRoundInfo(
                activeRound.Id,
                activeRound.Question.Description,
                activeRound.Question.Seconds,
                activeRound.Question.Options.Select(o => new QuestionOptionInfo(
                    o.Id,
                    o.Description
                )).ToArray(),
                activeRound.Startedat
            )
            : null;

        return new GameStateResponse(
            game.Id,
            game.Quiz.Name,
            game.Host.Name,
            players,
            currentRound,
            leaderboard,
            game.Gamerounds.Count
        );
    }

    private async Task<LeaderboardEntry[]> CalculateLeaderboard(string gameId)
    {
        var rounds = await repo.GameroundQuery()
            .Where(gr => gr.Gameid == gameId)
            .ToListAsync();

        var userScores = new Dictionary<string, (string userName, int score)>();

        foreach (var round in rounds)
        {
            foreach (var answer in round.Answers)
            {
                if (!userScores.ContainsKey(answer.Userid))
                {
                    userScores[answer.Userid] = (answer.User.Name, 0);
                }

                if (answer.OptionNavigation.Iscorrect)
                {
                    var (userName, currentScore) = userScores[answer.Userid];
                    userScores[answer.Userid] = (userName, currentScore + 1000);
                }
            }
        }

        return userScores
            .Select(kvp => new LeaderboardEntry(
                kvp.Key,
                kvp.Value.userName,
                kvp.Value.score
            ))
            .OrderByDescending(x => x.Score)
            .ToArray();
    }
}

// ============================================
// REQUEST DTOs
// ============================================

public record CreateGameRequest(string QuizId, string HostUserId);
public record JoinGameRequest(string GameId, string UserId);
public record StartRoundRequest(string GameId, string QuestionId);
public record SubmitAnswerRequest(string GameId, string RoundId, string UserId, string OptionId);
public record EndRoundRequest(string GameId, string RoundId);

// ============================================
// RESPONSE DTOs
// ============================================

public record CreateGameResponse(string GameId, string QuizName, int QuestionCount);
public record JoinGameResponse(string Status, string GameId, int PlayerCount);
public record StartRoundResponse(string RoundId, string Status);
public record SubmitAnswerResponse(string Status, bool IsCorrect);
public record EndRoundResponse(string Status, RoundResultEntry[] Results, LeaderboardEntry[] Leaderboard);

// ============================================
// Shared DTOs moved to api.Models.ReturnDtos.SharedDtos
// EVENT DTOs moved to api.Models.SseEventDtos
// ============================================