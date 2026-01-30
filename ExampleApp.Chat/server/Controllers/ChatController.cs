using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace server.Controllers;


public class ChatController(ISseBackplane backplane) : ControllerBase
{
    
    /*
     id: 1
       event: ConnectionResponse
       data: {"connectionId":"8cc4cabc-e550-4e20-9732-5da6282f573b","eventType":"ConnectionResponse"}
       
     */
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
        
        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }
    
    /*
     id: 2
       event: string
       data: {"members":["8cc4cabc-e550-4e20-9732-5da6282f573b"],"eventType":"JoinGroupResponse"}
       
     */
    [HttpPost(nameof(JoinGroup))]
    [Produces<JoinGroupResponse>]
    public async Task JoinGroup([FromBody] JoinGroupRequest request)
    {
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group);
        var members = await backplane.Groups.GetMembersAsync(request.Group);

        await backplane.Clients.SendToGroupAsync(request.Group,  new JoinGroupResponse()
        {
            Members = members.ToList()
        });

    }

    /*
     id: 3
       event: string
       data: {"connectionId":"8cc4cabc-e550-4e20-9732-5da6282f573b","message":"string","eventType":"MessageResponseDto"}
       
     */
    [HttpPost(nameof(SendMessageToGroup))]
    [Produces<MessageResponseDto>]
    public async Task SendMessageToGroup([FromBody] SendGroupMessageRequestDto dto)
    {
        await backplane.Clients.SendToGroupAsync(dto.GroupId, new MessageResponseDto
        {
            ConnectionId = dto.ConnectionId,
            Message = dto.Message
        });
    }
}


public record ConnectionResponse(string ConnectionId) : BaseResponseDto;


public record JoinGroupRequest(string ConnectionId, string Group);

public record SendGroupMessageRequestDto : BaseResponseDto
{
    public string Message { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public string GroupId { get; set; } = "";
}

public record JoinGroupResponse : BaseResponseDto
{
    public List<string> Members { get; set; }
}



public record MessageResponseDto : BaseResponseDto
{
    public string ConnectionId { get; set; } = "";
    public string Message { get; set; } = "";
}