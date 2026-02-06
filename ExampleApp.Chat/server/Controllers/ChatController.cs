using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

namespace server.Controllers;


public class ChatController(ISseBackplane backplane,
    JwtService jwtService,
    IRealtimeManager realtimeManager,
    MyDbContext ctx) : ControllerBase
{
    [HttpPost(nameof(Login))]
    public LoginResponse Login([FromBody] LoginRequest request)
    {
        var user = ctx.Users.FirstOrDefault(u => u.Nickname == request.Username) ??
                   throw new ValidationException("User does not exist");
        if(user.Hash == Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password + user.Salt))))
            return new LoginResponse(jwtService.GenerateToken(user.Id));
        throw new ValidationException("Not valid credentials");
    }
    [HttpPost(nameof(Register))]
    public LoginResponse Register([FromBody] LoginRequest request)
    {
        if (ctx.Users.Any(u => u.Nickname == request.Username))
            throw new ValidationException("Name is already taken");
        
        var salt = Guid.NewGuid().ToString();
        //im just using an arbitrary hashing algorithm
        var hash = Convert.ToBase64String(                                                                                                                                    
            SHA256.HashData(                                                                                                                     
                Encoding.UTF8.GetBytes(request.Password + salt)));   
        var u = new User()
        {
            Id = Guid.NewGuid().ToString(),
            Nickname = request.Username,
            Hash = hash,
            Salt = salt,
        };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return (new LoginResponse(jwtService.GenerateToken(u.Id)));
    }

    [HttpPost("listen/room-messages/{roomId}")]
    // [ProducesResponseType(typeof(List<Message>), 201)]
    public async Task<RealtimeListenResponse<List<Message>>> ListenToRoomMessages(string connectionId, string roomId)
    {
        var group = $"room-messages:{roomId}";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<MyDbContext>(group,
            criteria: changes =>
            {
                return changes.OfType<Message>()
                    .Any(e => e.State == EntityState.Added && e.Entity.RoomId == roomId);
            },
            query: async c => await c.Messages
                .Where(m => m.RoomId == roomId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync()
        );

        return new RealtimeListenResponse<List<Message>>(group, ctx.Messages
            .Where(m => m.RoomId == roomId)
            .ToList());
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
    
    
    /*[HttpPost(nameof(JoinGroup))]
    [ProducesResponseType(typeof(JoinGroupBroadcast), 202)]
    [ProducesResponseType(typeof(JoinGroupResponse), 200)]
    [ProducesResponseType(typeof(UserLeftResponseDto), 400)]

    public async Task<JoinGroupResponse> JoinGroup([FromBody] JoinGroupRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var u =  ctx.Users.FirstOrDefault(u => u.Id == userId);
        var room = ctx.Rooms.FirstOrDefault(r => r.Id == request.Group) ??
                   throw new ValidationException("Room does not exist");
        var name = u?.Nickname ?? "Anonymous";
        await backplane.Groups.AddToGroupAsync("nickname/"+request.ConnectionId, name);
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group);
        var members = await backplane.Groups.GetMembersAsync(request.Group);
        var list = new List<ConnectionIdAndUserName>();
        foreach (var m in members)
        {
            var nickname = await backplane.Groups.GetClientGroupsAsync("nickname/" + m);
            list.Add(new ConnectionIdAndUserName(m, nickname.FirstOrDefault() ?? "Anonymous"));
        }
        await backplane.Clients.SendToGroupAsync(request.Group, new JoinGroupBroadcast(list));
        
        return new JoinGroupResponse(room);


    }*/

    // [Authorize]
    // [HttpPost(nameof(Poke))]
    // [ProducesResponseType(typeof(PokeResponseDto), 200)]
    // public async Task Poke(PokeRequestDto dto)
    // {
    //     var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    //     var u =  ctx.Users.FirstOrDefault(u => u.Id == userId);
    //     var name = u?.Nickname ?? "Anonymous";
    //
    //     await backplane.Clients.SendToClientAsync(dto.connectionIdToPoke, new PokeResponseDto(name));
    // }

    [Authorize]
    [HttpPost(nameof(SendMessageToGroup))]
    public async Task SendMessageToGroup([FromBody] SendGroupMessageRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var message = new Message()
        {
            UserId = userId,
            Content = dto.Message,
            RoomId = dto.GroupId,
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Messages.Add(message);
        await ctx.SaveChangesAsync();
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
    // [ProducesResponseType(typeof(List<Room>), 201)]
    public async Task<RealtimeListenResponse<List<Room>>> GetRooms(string connectionId)
    {
        var group = "rooms";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<MyDbContext>(group,
            criteria: changes =>
            {
                return changes.HasAdded<Room>();
            },
            query:async (context) => await context.Rooms.ToListAsync()); 
        return new RealtimeListenResponse<List<Room>>(group, ctx.Rooms.ToList()); 
    }
    
    
}



public record PokeResponseDto(string pokedBy) : BaseResponseDto;
public record PokeRequestDto(string connectionIdToPoke);

public record JoinGroupResponse(Room room) : BaseResponseDto;
public record ConnectionResponse(string ConnectionId) : BaseResponseDto;


public record JoinGroupRequest(string ConnectionId, string Group);

public record SendGroupMessageRequestDto
{
    public string Message { get; set; } = "";
    public string GroupId { get; set; } = "";
}



public record MessageResponseDto : BaseResponseDto
{
    public string Message { get; set; } = "";
    public string? User { get; set; } = "";
}

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token);


public record JoinGroupBroadcast(List<ConnectionIdAndUserName> ConnectedUsers) : BaseResponseDto;

public record ConnectionIdAndUserName(string ConnectionId, string UserName);