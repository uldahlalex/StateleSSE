using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;

namespace server.Controllers;

[ApiController]
public class ChatController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet(nameof(StreamMessages)), SseEndpoint]
    [Produces<Message>]
    public async Task StreamMessages(string groupId)
    {
        var channel = $"chat:{groupId}:Message";
        await HttpContext.StreamSseAsync<Message>(backplane, channel);
    }

    [HttpPost(nameof(CreateMessage))]
    [ProducesResponseType<Message>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMessage([FromBody] CreateMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ErrorResponse("Content cannot be empty", "EMPTY_CONTENT"));

        if (request.Content.Length > 500)
            return BadRequest(new ErrorResponse("Content too long", "CONTENT_TOO_LONG"));

        var channel = $"chat:{request.GroupId}:Message";
        var message = new Message { Content = request.Content };

        await backplane.PublishToGroup(channel, message);

        return Ok(message);
    }
}

public class Message
{
    public required string Content { get; set; }
}

public record CreateMessageRequest(string Content, string GroupId);
public record ErrorResponse(string Message, string? Code = null);