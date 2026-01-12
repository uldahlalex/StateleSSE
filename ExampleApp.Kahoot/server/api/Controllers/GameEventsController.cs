using api.Models.SseEventDtos;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace api.Controllers;

// Request DTOs for SSE subscriptions
public record RoundStartedSubscribeRequestDto(string GameId);
public record RoundEndedSubscribeRequestDto(string GameId);
public record PlayerJoinedSubscribeRequestDto(string GameId);
public record AnswerSubmittedSubscribeRequestDto(string GameId);
public record GameCreatedSubscribeRequestDto(string GameId);

/// <summary>
/// DTO-CENTRIC ARCHITECTURE: Each endpoint streams exactly ONE event type
/// Clients subscribe to specific event types they need, composing their own event streams
/// </summary>
[ApiController]
public class GameEventsController(ISseBackplane backplane)
    : SseControllerBase(backplane)
{
    /// <summary>
    /// Stream RoundStartedEvent only
    /// Use: When round begins, clients need question details
    /// </summary>
    [HttpGet(nameof(RoundStartedEvent))]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(RoundStartedEvent), 200)]
    public async Task StreamRoundStarted([FromQuery] RoundStartedSubscribeRequestDto dto)
    {
        var channel = $"game:{dto.GameId}:RoundStartedEvent";
        await StreamEventType<RoundStartedEvent>(channel);
    }

    /// <summary>
    /// Stream PlayerJoinedEvent only
    /// Use: Track player joins in lobby
    /// </summary>
    [HttpGet(nameof(PlayerJoinedEvent))]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(PlayerJoinedEvent), 200)]
    public async Task StreamPlayerJoined([FromQuery] PlayerJoinedSubscribeRequestDto dto)
    {
        var channel = $"game:{dto.GameId}:PlayerJoinedEvent";
        await StreamEventType<PlayerJoinedEvent>(channel);
    }

    /// <summary>
    /// Stream AnswerSubmittedEvent only
    /// Use: Host dashboard showing answer progress
    /// </summary>
    [HttpGet(nameof(AnswerSubmittedEvent))]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(AnswerSubmittedEvent), 200)]
    public async Task StreamAnswerSubmitted([FromQuery] AnswerSubmittedSubscribeRequestDto dto)
    {
        var channel = $"game:{dto.GameId}:AnswerSubmittedEvent";
        await StreamEventType<AnswerSubmittedEvent>(channel);
    }

    /// <summary>
    /// Stream RoundEndedEvent only
    /// Use: Display results and leaderboard after round
    /// </summary>
    [HttpGet(nameof(RoundEndedEvent))]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(RoundEndedEvent), 200)]
    public async Task StreamRoundEnded([FromQuery] RoundEndedSubscribeRequestDto dto)
    {
        var channel = $"game:{dto.GameId}:RoundEndedEvent";
        await StreamEventType<RoundEndedEvent>(channel);
    }

    /// <summary>
    /// Stream GameCreatedEvent only
    /// Use: Initial game setup notifications
    /// </summary>
    [HttpGet(nameof(GameCreatedEvent))]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(GameCreatedEvent), 200)]
    public async Task StreamGameCreated([FromQuery] GameCreatedSubscribeRequestDto dto)
    {
        var channel = $"game:{dto.GameId}:GameCreatedEvent";
        await StreamEventType<GameCreatedEvent>(channel);
    }
}
