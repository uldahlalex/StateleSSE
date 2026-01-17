using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace server.Controllers;

[ApiController]
public class ChatControllerMinimal(ISseBackplane backplane) : ControllerBase
{
    [HttpGet(nameof(StreamMessages))]
    [Produces<Message>]
    [EventSourceEndpoint(typeof(Message))]
    public async Task StreamMessages(string groupId)
    {
        var channel = $"chat:{groupId}:Message";
        await HttpContext.StreamSseAsync<Message>(backplane, channel, keepaliveInterval: TimeSpan.FromSeconds(30));
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
