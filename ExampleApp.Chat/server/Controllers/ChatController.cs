using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;
using ExampleApp.Chat;

public class ChatController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// SSE stream endpoint. Subscribes to channels specified in query params.
    /// Example: /events?channels=rooms/123/messages,rooms/123/typing
    /// </summary>
    [HttpGet("")]
    [Produces("text/event-stream")]
    public async Task Events([FromQuery] string channels)
    {
        var channelList = channels?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
       
        await using var sse = await HttpContext.OpenSseStreamAsync();
        using var connection = backplane.CreateConnection();                                                                                                                                 

        foreach (var channel in channelList)
        {
            connection.Subscribe(channel);                                                                                                                                                
            
        }
        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))                                                                                                        
        {                                                                                                                                                                             
            await sse.WriteAsync(evt.Channel, evt.Data);                                                                                                                              
        } 
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

        await backplane.Publish(Channels.Resolve(Channels.RoomMessages, roomId), message);
        return Ok(message);
    }

    /// <summary>
    /// Send a typing indicator.
    /// </summary>
    [HttpPost("rooms/{roomId}/typing")]
    public async Task<IActionResult> SendTyping(string roomId, [FromBody] TypingRequest request)
    {
        await backplane.Publish(Channels.Resolve(Channels.RoomTyping, roomId), new TypingEvent
        {
            RoomId = roomId,
            Username = request.Username,
            IsTyping = request.IsTyping
        });
        return Ok();
    }

    /// <summary>
    /// Update presence status.
    /// </summary>
    [HttpPost("rooms/{roomId}/presence")]
    public async Task<IActionResult> UpdatePresence(string roomId, [FromBody] PresenceRequest request)
    {
        await backplane.Publish(Channels.Resolve(Channels.RoomPresence, roomId), new PresenceEvent
        {
            RoomId = roomId,
            Username = request.Username,
            Online = request.Online
        });
        return Ok();
    }
}

// Request DTOs
public record SendMessageRequest(string Author, string Content);
public record TypingRequest(string Username, bool IsTyping);
public record PresenceRequest(string Username, bool Online);

// Event DTOs (published to channels)
public class ChatMessage
{
    public required string Id { get; set; }
    public required string RoomId { get; set; }
    public required string Author { get; set; }
    public required string Content { get; set; }
    public required string Timestamp { get; set; }
}

public class TypingEvent
{
    public required string RoomId { get; set; }
    public required string Username { get; set; }
    public required bool IsTyping { get; set; }
}

public class PresenceEvent
{
    public required string RoomId { get; set; }
    public required string Username { get; set; }
    public required bool Online { get; set; }
}
