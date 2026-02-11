using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfBackplane;
using StateleSSE.AspNetCore.EfRealtime;

namespace server.Controllers;


public class ChatController(ISseBackplane backplane,
    JwtService jwtService,
    IRealtimeManager realtimeManager,
    MyDbContext ctx) : RealtimeControllerBase(backplane)
{
    protected override string? GetConnectionUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);


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

    [HttpGet(nameof(GetMessages))]
    public async Task<RealtimeListenResponse<List<Message>>> GetMessages(string connectionId, string roomId)
    {
        var group = "message:"+roomId;
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<MyDbContext>(connectionId, group,
            criteria: changes =>
            {
                var change = changes.HasAdded<Message>();
                return change;
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

    [HttpPatch(nameof(UpdateMessage))]
    public async Task UpdateMessage([FromBody]Message newMessage)
    {
        var message = ctx.Messages.FirstOrDefault(m => m.Id == newMessage.Id)
                      ?? throw new ValidationException("message not found");
        ctx.Entry(message).CurrentValues.SetValues(newMessage);
        await ctx.SaveChangesAsync();
    }

    [Authorize]
    [HttpPost(nameof(CreateMessage))]
    public async Task CreateMessage([FromBody] CreateMessageRequestDto dto)
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
    public async Task CreateRoom(string name)
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
    }
    
    [Authorize]
    [HttpPost(nameof(DeleteRoom))]
    public async Task DeleteRoom(string id)
    {
        var room = await ctx.Rooms.FindAsync(id);
        if (room is null)
            return;
        ctx.Rooms.Remove(room);
         ctx.SaveChanges();
    }

    [Authorize]                                                                                                  
    [HttpPost(nameof(UpdateRoom))]
    public async Task UpdateRoom([FromBody]Room newRoom)                                                        
    {                                                                                  
        var room = await ctx.Rooms.FindAsync(newRoom.Id);                                                                
        if (room is null)                                                                                        
            return;
        ctx.Entry(room).CurrentValues.SetValues(newRoom);
        await ctx.SaveChangesAsync();
    }


    
    [HttpGet(nameof(GetRooms))]
    public async Task<RealtimeListenResponse<List<Room>>> GetRooms(string connectionId)
    {
        var group = "rooms";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<MyDbContext>(connectionId, "rooms",
            criteria: changes =>
            {
                return changes.OfType<Room>().Any();
            },
            query:async (context) => await context.Rooms.ToListAsync()); 
        return new RealtimeListenResponse<List<Room>>(group, ctx.Rooms.ToList()); 
    }
    
    [HttpGet(nameof(GetMembers))]
    public async Task<RealtimeListenResponse<List<MemberInfo>>> GetMembers(string connectionId, string roomId)
    {
        var listenGroup = "members:"+roomId;
        await backplane.Groups.AddToGroupAsync(connectionId, listenGroup);

        realtimeManager.Subscribe<MyDbContext>(connectionId, listenGroup,
            criteria: changes => changes.HasChanges<SseGroupMember>() || changes.HasChanges<SseConnection>(),
            query: async c => await GetMembersQuery(c, listenGroup));

        return new RealtimeListenResponse<List<MemberInfo>>(listenGroup, await GetMembersQuery(ctx, listenGroup));
    }

    private static async Task<List<MemberInfo>> GetMembersQuery(MyDbContext c, string listenGroup) =>
        await c.SseGroupMembers
            .Where(gm => gm.GroupName == listenGroup)
            .Join(c.SseConnections, gm => gm.ConnectionId, conn => conn.ConnectionId, (gm, conn) => conn)
            .Join(c.Users, conn => conn.UserId, u => u.Id, (conn, user) => new MemberInfo(conn.ConnectionId, user.Nickname))
            .ToListAsync();
    

    [HttpGet(nameof(GetPokes))]                               
    public async Task<RealtimeListenResponse<object?>> GetPokes(string connectionId)                             
    {                                                                                                            
        var group = $"poke:{connectionId}";
        await backplane.Groups.AddToGroupAsync(connectionId, group);
        return new RealtimeListenResponse<object?>(group, null);
    }

    [HttpPost(nameof(Poke))]
    public async Task Poke(string connectionId)
    {
        await backplane.Clients.SendToGroupAsync($"poke:{connectionId}", new { message = "You have been poked"
        });
    }

    

    
}