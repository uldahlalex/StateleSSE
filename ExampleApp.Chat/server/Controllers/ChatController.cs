using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

public class ChatController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet(nameof(Connect))]
    [Produces<ConnectionResponse>]
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync();
        await using var connection = backplane.CreateConnection();

        await sse.WriteAsync(nameof(ConnectionResponse), JsonSerializer.Serialize(new ConnectionResponse(connection.ConnectionId), new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        await backplane.Clients.ClientAsync(connection.ConnectionId, new ConnectionResponse(connection.ConnectionId));
        
        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }

    /// <summary>
    /// Join a group. Single subscription - all event types come through the same group.
    /// </summary>
    [HttpPost(nameof(JoinGroup))]
    [Produces<JoinedPayload>]
    public async Task JoinGroup([FromBody] JoinGroupRequest request)
    {
        // Single group subscription (not group/joined + group/message)
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group);
        var members = await backplane.Groups.GetMembersAsync(request.Group);

        // Event type is in the payload, not the group name
        await backplane.Clients.GroupAsync(request.Group,  new JoinedPayload
            {
                ConnectionId = request.ConnectionId,
                MemberCount = members.Count
            });

    }

    [HttpPost(nameof(SendMessageToGroup))]
    [Produces<MessagePayload>]
    public async Task SendMessageToGroup([FromBody] SendGroupMessageRequestDto dto)
    {
        // Event type is in the payload
        await backplane.Clients.GroupAsync(dto.GroupId, new MessagePayload
        {
            ConnectionId = dto.ConnectionId,
            Message = dto.Message
        });
    }
}


public abstract record BaseResponseDto
{
    public BaseResponseDto()
    {
        EventType = GetType().Name;
       
    }

    public string EventType { get; set; }
}

// Connection
public record ConnectionResponse(Guid ConnectionId) : BaseResponseDto;

// Requests
public record JoinGroupRequest(Guid ConnectionId, string Group);

public record SendGroupMessageRequestDto : BaseResponseDto
{
    public string Message { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public string GroupId { get; set; } = "";
}

// Responses
public record JoinGroupResponse : BaseResponseDto
{
    public string Group { get; set; } = "";
    public int MemberCount { get; set; }
}


// Event payloads
public record JoinedPayload : BaseResponseDto
{
    public Guid ConnectionId { get; set; }
    public int MemberCount { get; set; }
}

public record MessagePayload : BaseResponseDto
{
    public string ConnectionId { get; set; } = "";
    public string Message { get; set; } = "";
}
