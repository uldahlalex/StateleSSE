using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

[ApiController]
[Route("api/chat")]
public class ChatController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// SSE stream endpoint. Returns connectionId as first event.
    /// </summary>
    [HttpGet("events")]
    [Produces("text/event-stream")]
    [Produces<Guid>]
    public async Task Events()
    {
        var connectionId = await HttpContext.StreamSseAsync(backplane);
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

    /// <summary>
    /// Send a message to a chat room.
    /// </summary>
    [HttpPost("rooms/{roomId}/messages")]
    public async Task<IActionResult> SendMessage(string roomId, [FromBody] SendMessageRequest request)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            Author = request.Author,
            Content = request.Content,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        await backplane.Publish($"chat:{roomId}:messages", message);
        return Ok(message);
    }

    /// <summary>
    /// Send a typing indicator.
    /// </summary>
    [HttpPost("rooms/{roomId}/typing")]
    public async Task<IActionResult> SendTyping(string roomId, [FromBody] TypingRequest request)
    {
        await backplane.Publish($"chat:{roomId}:typing", new
        {
            roomId,
            request.Username,
            request.IsTyping
        });
        return Ok();
    }

    /// <summary>
    /// Update presence status.
    /// </summary>
    [HttpPost("rooms/{roomId}/presence")]
    public async Task<IActionResult> UpdatePresence(string roomId, [FromBody] PresenceRequest request)
    {
        await backplane.Publish($"chat:{roomId}:presence", new
        {
            roomId,
            request.Username,
            request.Online
        });
        return Ok();
    }
}

// Request DTOs
public record SubscribeRequest(Guid ConnectionId, string Channel);
public record UnsubscribeRequest(Guid ConnectionId, string Channel);
public record SendMessageRequest(string Author, string Content);
public record TypingRequest(string Username, bool IsTyping);
public record PresenceRequest(string Username, bool Online);

// Response DTOs
public class ChatMessage
{
    public required string Id { get; set; }
    public required string RoomId { get; set; }
    public required string Author { get; set; }
    public required string Content { get; set; }
    public required string Timestamp { get; set; }
}
