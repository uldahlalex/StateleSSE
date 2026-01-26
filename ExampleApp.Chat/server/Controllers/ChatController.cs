using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;
using ExampleApp.Chat;

public class ChatController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// SSE stream endpoint. Subscribes to groups specified in query params.
    /// Example: /events?channels=rooms/123/messages,rooms/123/typing
    /// </summary>
    [HttpGet("")]
    [Produces("text/event-stream")]
    public async Task Events([FromQuery] string channels)
    {
        var groupList = channels?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        await using var sse = await HttpContext.OpenSseStreamAsync();
        await using var connection = backplane.CreateConnection();

        foreach (var group in groupList)
        {
            await connection.JoinGroupAsync(group);
        }

        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
            {
                await sse.WriteAsync(evt.Group, evt.Data);
            }
            else
            {
                await sse.WriteAsync(evt.Data);
            }
        }
    }

    /// <summary>
    /// Send a message to a chat room.
    /// </summary>
    [HttpPost("rooms/{roomId}/messages")]
    public async Task SendMessage(string roomId, [FromBody] SendMessageRequest request)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            Author = request.Author,
            Content = request.Content,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        await backplane.Clients.GroupAsync("rooms/" + roomId + "/messages", message);
    }

    /// <summary>
    /// Send a typing indicator.
    /// </summary>
    [HttpPost("rooms/{roomId}/typing")]
    public async Task SendTyping(string roomId, [FromBody] TypingRequest request)
    {
        await backplane.Clients.GroupAsync("rooms/" + roomId + "/typing", new TypingEvent
        {
            RoomId = roomId,
            Username = request.Username,
            IsTyping = request.IsTyping
        });
    }

    /// <summary>
    /// Update presence status. Returns the number of online users in the room.
    /// </summary>
    [HttpPost("rooms/{roomId}/presence")]
    public async Task<IActionResult> UpdatePresence(string roomId, [FromBody] PresenceRequest request)
    {
        // Get the count of clients in this room's presence group
        var onlineCount = await backplane.Groups.GetMemberCountAsync("rooms/" + roomId + "/presence");

        await backplane.Clients.GroupAsync("rooms/" + roomId + "/presence", new PresenceEvent
        {
            RoomId = roomId,
            OnlineUsers = onlineCount
        });

        return Ok(new { OnlineUsers = onlineCount });
    }
}

// Request DTOs
public record SendMessageRequest(string Author, string Content);
public record TypingRequest(string Username, bool IsTyping);
public record PresenceRequest(string Username, bool Online);

// Event DTOs (published to groups)
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
    public required int OnlineUsers { get; set; }
}
