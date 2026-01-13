using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace server.Controllers;

[ApiController]
public class ChatController(ISseBackplane backplane) : SseControllerBase(backplane)
{
    [HttpGet(nameof(StreamMessages))]
    [EventSourceEndpoint(typeof(Message))]
    [ProducesResponseType(typeof(Message), 200)]
    public async Task StreamMessages(string groupId)
    {
        var channel = $"chat:{groupId}:Message";
        await StreamEventType<Message>(channel);
    }

    [HttpPost(nameof(CreateMessage))]
    public async Task CreateMessage(string content, string groupId)
    {
        var channel = $"chat:{groupId}:Message";
        await backplane.PublishToGroup(channel, new Message
        {
            Content = content
        });
    }
}

public class Message
{
    public required string Content { get; set; }
}