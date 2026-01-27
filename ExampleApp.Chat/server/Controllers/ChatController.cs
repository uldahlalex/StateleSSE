using System.Text.Json;
using ExampleApp.Chat.Controllers;
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
        
        await sse.WriteAsync("connected", JsonSerializer.Serialize(new ConnectionResponse(connection.ConnectionId)));
        
        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }   
    
    /// <summary>
    /// Join a group.
    /// </summary>
    [HttpPost(nameof(JoinGroup))]
    public async Task JoinGroup([FromBody] JoinGroupRequest request)
    {
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group+"/joined");
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group+"/message");
        var members = await backplane.Groups.GetMembersAsync(request.Group+"/joined");

        await backplane.Clients.GroupAsync(request.Group+"/joined", new JoinGroupResponse()
        {
            Group = request.Group,
            MemberCount = members.Count
        });
    }

    [HttpPost(nameof(SendMessageToGroup))]
    public async Task SendMessageToGroup([FromBody]SendGroupMessageRequestDto dto)
    {
        await backplane.Clients.GroupAsync(dto.GroupId+"/message", dto);
    }
    
    

}

public record ConnectionResponse(Guid connectionId);

public class SendGroupMessageRequestDto
{
    public string Message { get; set; }
    public string ConnectionId { get; set; }
    public string GroupId { get; set; }
}