using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

[ApiController]
[Route("api/chat")]
public class ChatController(ISseBackplane backplane) : ControllerBase
{
    // ========================================
    // SSE Connection & Subscription Endpoints
    // ========================================

    /// <summary>
    /// Opens an SSE stream connection. Returns connectionId as first event.
    /// Client uses this connectionId to subscribe to channels via other endpoints.
    /// </summary>
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    public async Task Stream()
    {
        await HttpContext.StreamSseAsync(backplane);
    }

    /// <summary>
    /// Subscribe to messages in a chat room.
    /// </summary>
    [HttpPost("subscribe/messages")]
    public IActionResult SubscribeToMessages([FromBody] SubscribeRequest request)
    {
        var channel = $"chat:{request.RoomId}:messages";
        var success = backplane.AddSubscription(request.ConnectionId, channel);
        return success ? Ok() : NotFound("Connection not found");
    }

    /// <summary>
    /// Subscribe to typing indicators in a chat room.
    /// </summary>
    [HttpPost("subscribe/typing")]
    public IActionResult SubscribeToTyping([FromBody] SubscribeRequest request)
    {
        var channel = $"chat:{request.RoomId}:typing";
        var success = backplane.AddSubscription(request.ConnectionId, channel);
        return success ? Ok() : NotFound("Connection not found");
    }

    /// <summary>
    /// Subscribe to presence updates in a chat room.
    /// </summary>
    [HttpPost("subscribe/presence")]
    public IActionResult SubscribeToPresence([FromBody] SubscribeRequest request)
    {
        var channel = $"chat:{request.RoomId}:presence";
        var success = backplane.AddSubscription(request.ConnectionId, channel);
        return success ? Ok() : NotFound("Connection not found");
    }

    /// <summary>
    /// Unsubscribe from a channel.
    /// </summary>
    [HttpPost("unsubscribe")]
    public IActionResult Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        var success = backplane.RemoveSubscription(request.ConnectionId, request.Channel);
        return success ? Ok() : NotFound("Connection or subscription not found");
    }

    /// <summary>
    /// Generic subscribe to any channel (for statelesse-react library).
    /// </summary>
    [HttpPost("subscribe")]
    public IActionResult Subscribe([FromBody] GenericSubscribeRequest request)
    {
        var success = backplane.AddSubscription(request.ConnectionId, request.Channel);
        return success ? Ok() : NotFound("Connection not found");
    }

    // ========================================
    // Chat Actions
    // ========================================

    /// <summary>
    /// Send a message to a chat room.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = request.RoomId,
            Author = request.Author,
            Content = request.Content,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        var channel = $"chat:{request.RoomId}:messages";
        var envelope = new EventEnvelope
        {
            Channel = channel,
            Payload = message
        };

        await backplane.PublishToGroup(channel, envelope);
        return Ok(message);
    }

    /// <summary>
    /// Send a typing indicator.
    /// </summary>
    [HttpPost("typing")]
    public async Task<IActionResult> SendTyping([FromBody] TypingRequest request)
    {
        var channel = $"chat:{request.RoomId}:typing";
        var envelope = new EventEnvelope
        {
            Channel = channel,
            Payload = new TypingIndicator
            {
                RoomId = request.RoomId,
                Username = request.Username,
                IsTyping = request.IsTyping
            }
        };

        await backplane.PublishToGroup(channel, envelope);
        return Ok();
    }

    /// <summary>
    /// Update presence status.
    /// </summary>
    [HttpPost("presence")]
    public async Task<IActionResult> UpdatePresence([FromBody] PresenceRequest request)
    {
        var channel = $"chat:{request.RoomId}:presence";
        var envelope = new EventEnvelope
        {
            Channel = channel,
            Payload = new PresenceUpdate
            {
                RoomId = request.RoomId,
                Username = request.Username,
                Online = request.Online
            }
        };

        await backplane.PublishToGroup(channel, envelope);
        return Ok();
    }

    // ========================================
    // Legacy endpoint (backwards compatibility)
    // ========================================

    /// <summary>
    /// Legacy: Stream messages directly (old pattern).
    /// </summary>
    [HttpGet("legacy/messages")]
    [Produces("text/event-stream")]
    public async Task StreamMessagesLegacy([FromQuery] string roomId)
    {
        var channel = $"chat:{roomId}:messages";
        await HttpContext.StreamSseAsync<ChatMessage>(backplane, channel);
    }
}

// ========================================
// Request DTOs
// ========================================

public record SubscribeRequest(Guid ConnectionId, string RoomId);
public record GenericSubscribeRequest(Guid ConnectionId, string Channel);
public record UnsubscribeRequest(Guid ConnectionId, string Channel);
public record SendMessageRequest(string RoomId, string Author, string Content);
public record TypingRequest(string RoomId, string Username, bool IsTyping);
public record PresenceRequest(string RoomId, string Username, bool Online);

// ========================================
// Event DTOs
// ========================================

public class EventEnvelope
{
    public required string Channel { get; set; }
    public required object Payload { get; set; }
}

public class ChatMessage
{
    public required string Id { get; set; }
    public required string RoomId { get; set; }
    public required string Author { get; set; }
    public required string Content { get; set; }
    public required string Timestamp { get; set; }
}

public class TypingIndicator
{
    public required string RoomId { get; set; }
    public required string Username { get; set; }
    public required bool IsTyping { get; set; }
}

public class PresenceUpdate
{
    public required string RoomId { get; set; }
    public required string Username { get; set; }
    public required bool Online { get; set; }
}
