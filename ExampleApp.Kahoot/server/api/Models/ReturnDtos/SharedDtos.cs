namespace api.Models.ReturnDtos;

public record QuestionOptionInfo(
    string Id,
    string Text);

public record LeaderboardEntry(
    string UserId,
    string UserName,
    int Score);

public record PlayerInfo(
    string UserId,
    string UserName,
    DateTime JoinedAt);

public record CurrentRoundInfo(
    string RoundId,
    string QuestionText,
    int TimeLimit,
    QuestionOptionInfo[] Options,
    DateTime StartedAt);

public record GameStateResponse(
    string GameId,
    string QuizName,
    string HostName,
    PlayerInfo[] Players,
    CurrentRoundInfo? CurrentRound,
    LeaderboardEntry[] Leaderboard,
    int TotalRounds);
