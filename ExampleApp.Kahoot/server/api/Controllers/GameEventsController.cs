using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace api.Controllers;

/// <summary>
/// SSE streaming endpoints for Kahoot game events.
/// Uses simplified StateleSSE API with channel-based routing.
/// </summary>
[ApiController]
[Route("api/game")]
public class GameEventsController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// SSE stream endpoint. Returns connectionId as first event.
    /// Client subscribes to channels like "game:{gameId}:events" to receive game events.
    /// </summary>
    [HttpGet("events")]
    [Produces("text/event-stream")]
    public async Task Events()
    {
        await HttpContext.StreamSseAsync(backplane);
    }

    /// <summary>
    /// Subscribe to a channel.
    /// </summary>
    [HttpPost("subscribe")]
    public IActionResult Subscribe([FromBody] SubscribeRequest request)
    {
        var success = backplane.Subscribe(request.ConnectionId, request.Channel);
        return success ? Ok() : NotFound("Connection not found");
    }

    /// <summary>
    /// Unsubscribe from a channel.
    /// </summary>
    [HttpPost("unsubscribe")]
    public IActionResult Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        var success = backplane.Unsubscribe(request.ConnectionId, request.Channel);
        return success ? Ok() : NotFound("Connection or subscription not found");
    }
}

public record SubscribeRequest(Guid ConnectionId, string Channel);
public record UnsubscribeRequest(Guid ConnectionId, string Channel);
