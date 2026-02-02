using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace server.Controllers;


public class ChatController(ISseBackplane backplane,
    MyDbContext ctx) : ControllerBase
{
    [HttpPost(nameof(Login))]
    public LoginResponse Login([FromBody] LoginRequest request)
    {
        if (request.Username == "test" && request.Password == "test")
            return (new LoginResponse(JwtService.GenerateToken(request.Username)));
        throw new ValidationException("Not valid credentials");
    }

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
    [Authorize]
    [HttpPost(nameof(SendMessageToGroup))]
    [Produces<MessageResponseDto>]
    public async Task SendMessageToGroup([FromBody] SendGroupMessageRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var message = new Message()
        {
            UserId = userId,
            Content = dto.Message,
            RoomId = dto.GroupId,
            Id = Guid.NewGuid().ToString(),
        };
        ctx.Messages.Add(message);
        await ctx.SaveChangesAsync();
        await backplane.Clients.SendToGroupAsync(dto.GroupId, new MessageResponseDto
        {
            User = userId,
            Message = dto.Message
        });
    }

    [Authorize]
    [HttpPost(nameof(CreateRoom))]
    public async Task<Room> CreateRoom(string name)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);    
        var room = new Room()
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            CreatedBy = userId
        };
        ctx.Rooms.Add(room);
        await ctx.SaveChangesAsync();
        return room;
    }


    [HttpGet(nameof(GetRooms))]
    public async Task<List<Room>> GetRooms()
        => ctx.Rooms.ToList();
}


public record ConnectionResponse(string ConnectionId) : BaseResponseDto;


public record JoinGroupRequest(string ConnectionId, string Group);

public record SendGroupMessageRequestDto
{
    public string Message { get; set; } = "";
    public string GroupId { get; set; } = "";
}

public record JoinGroupResponse : BaseResponseDto
{
    public List<string> Members { get; set; }
}



public record MessageResponseDto : BaseResponseDto
{
    public string Message { get; set; } = "";
    public string? User { get; set; } = "";
}

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token);