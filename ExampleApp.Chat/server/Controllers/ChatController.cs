using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

public class ChatController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet(nameof(StreamMessages))]
    [Produces<Message>]
    public async Task StreamMessages(string groupId)
    {
        var channel = $"chat:{groupId}:Message";
        await HttpContext.StreamSseAsync<Message>(backplane, channel);
    }

    [HttpPost(nameof(CreateMessage))]
    public async Task CreateMessage([FromBody] CreateMessageRequest request)
    {
        var channel = $"chat:{request.GroupId}:Message";
        var message = new Message { Content = request.Content };
        await backplane.PublishToGroup(channel, message);
    }
}

public class Message
{
    public required string Content { get; set; }
}

public record CreateMessageRequest(string Content, string GroupId);
