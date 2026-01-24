using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

public class ChatController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// SSE stream endpoint. Subscribes to channels specified in query params.
    /// Example: /events?channel=chat:room1:messages&amp;channel=chat:room1:typing
    /// </summary>
    [HttpGet("")]
    [Produces("text/event-stream")]
    public async Task Events([FromQuery] string[] channel)
    {
        await HttpContext.StreamSseAsync(backplane, channel);
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
