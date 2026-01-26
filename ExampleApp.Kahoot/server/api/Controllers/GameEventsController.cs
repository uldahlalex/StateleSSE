using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace api.Controllers;

/// <summary>
/// SSE streaming endpoints for Kahoot game events.
/// Uses simplified StateleSSE API with group-based routing.
/// </summary>
[ApiController]
[Route("api/game")]
public class GameEventsController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// SSE stream endpoint. Returns connectionId as first event.
    /// Client subscribes to groups like "game:{gameId}:events" to receive game events.
    /// </summary>
    [HttpGet("events")]
    [Produces("text/event-stream")]
    public async Task Events(string[] channels)
    {
        await HttpContext.StreamSseAsync(backplane, channels);
    }

    /// <summary>
    /// Subscribe to a group.
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Channel);
        return Ok();
    }

    /// <summary>
    /// Unsubscribe from a group.
    /// </summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        await backplane.Groups.RemoveFromGroupAsync(request.ConnectionId, request.Channel);
        return Ok();
    }
}

public record SubscribeRequest(Guid ConnectionId, string Channel);
public record UnsubscribeRequest(Guid ConnectionId, string Channel);
