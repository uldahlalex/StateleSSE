using api.Models.ReturnDtos;

namespace api.Models.SseEventDtos;

public record RoundResultEntry(
    string UserId,
    string UserName,
    string OptionId,
    bool IsCorrect,
    DateTime? AnsweredAt);

/// <summary>
/// Generic SSE event envelope wrapper
/// </summary>
public record SseEventEnvelope<T>(string Type, T Data);

/// <summary>
/// Union type containing all possible SSE event payloads.
/// This is used to expose event types to NSwag for TypeScript generation.
/// The actual SSE stream sends JSON objects matching one of these event types.
/// </summary>
public class GameEventUnion
{
    public string Type { get; set; } = string.Empty;
    public GameCreatedEvent? GameCreated { get; set; }
    public PlayerJoinedEvent? PlayerJoined { get; set; }
    public RoundStartedEvent? RoundStarted { get; set; }
    public AnswerSubmittedEvent? AnswerSubmitted { get; set; }
    public RoundEndedEvent? RoundEnded { get; set; }
    public SseEventEnvelope<GameStateResponse>? GameState { get; set; }
}

public record GameCreatedEvent(
    string GameId,
    string QuizName,
    string HostId,
    int QuestionCount,
    DateTime Timestamp)
{
    public string Type => "game_created";
}

public record PlayerJoinedEvent(
    string UserId,
    string UserName,
    int PlayerCount,
    DateTime Timestamp)
{
    public string Type => "player_joined";
}

public record RoundStartedEvent(
    string RoundId,
    string QuestionId,
    string QuestionText,
    int TimeLimit,
    QuestionOptionInfo[] Options,
    DateTime Timestamp)
{
    public string Type => "round_started";
}

public record AnswerSubmittedEvent(
    string UserId,
    int AnswersReceived,
    DateTime Timestamp)
{
    public string Type => "answer_submitted";
}

public record RoundEndedEvent(
    string RoundId,
    string CorrectOptionId,
    RoundResultEntry[] Results,
    LeaderboardEntry[] Leaderboard,
    DateTime Timestamp)
{
    public string Type => "round_ended";
}

public record WeatherDataEvent(
    string StationId,
    string StationName,
    decimal Temperature,
    decimal Humidity,
    decimal Pressure,
    DateTime Timestamp)
{
    public string Type => "weather_data";
}
