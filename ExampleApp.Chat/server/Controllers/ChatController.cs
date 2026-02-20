using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

namespace server.Controllers;

public class ChatController(
    IRealtimeManager realtimeManager,
    JwtService jwtService,
    MyDbContext ctx) : RealtimeControllerBase(realtimeManager)
{
    [HttpPost(nameof(GuestLogin))]
    public LoginResponse GuestLogin() =>
        new LoginResponse(jwtService.GenerateToken(Guid.NewGuid().ToString()));

    [HttpPost(nameof(Login))]
    public LoginResponse Login([FromBody] LoginRequest request)
    {
        var user = ctx.Users.FirstOrDefault(u => u.Nickname == request.Username) ??
                   throw new ValidationException("User does not exist");
        if (user.Hash == Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password + user.Salt))))
            return new LoginResponse(jwtService.GenerateToken(user.Id));
        throw new ValidationException("Not valid credentials");
    }

    [HttpPost(nameof(Register))]
    public LoginResponse Register([FromBody] LoginRequest request)
    {
        if (ctx.Users.Any(u => u.Nickname == request.Username))
            throw new ValidationException("Name is already taken");
        var salt = Guid.NewGuid().ToString();
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password + salt)));
        var u = new User { Id = Guid.NewGuid().ToString(), Nickname = request.Username, Hash = hash, Salt = salt };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return new LoginResponse(jwtService.GenerateToken(u.Id));
    }

    [HttpGet(nameof(GetMessages))]
    [ProducesResponseType<List<Message>>(200)]
    public Task GetMessages(string roomId, CancellationToken ct) =>
        ListenAsync<MyDbContext, List<Message>>(
            getInitialData: () => ctx.Messages.Where(m => m.RoomId == roomId).OrderByDescending(m => m.CreatedAt).ToListAsync(),
            criteria: changes => changes.HasAdded<Message>(),
            query: async c => await c.Messages.Where(m => m.RoomId == roomId).OrderByDescending(m => m.CreatedAt).ToListAsync(),
            ct);

    [HttpGet(nameof(GetRooms))]
    [ProducesResponseType<List<Room>>(200)]
    public Task GetRooms(CancellationToken ct) =>
        ListenAsync<MyDbContext, List<Room>>(
            getInitialData: () => ctx.Rooms.ToListAsync(),
            criteria: changes => changes.HasChanges<Room>(),
            query: async c => await c.Rooms.ToListAsync(),
            ct);

    [HttpGet(nameof(GetMembers))]
    [ProducesResponseType<List<MemberInfo>>(200)]
    public async Task GetMembers(string roomId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var conn  = new SseConnectionMetadata      { ConnectionId = Guid.NewGuid().ToString(), ServerId = Environment.MachineName, LastSeen = DateTimeOffset.UtcNow, OwnerId = userId };
        var group = new SseConnectionMetadataGroup { ConnectionId = conn.ConnectionId, GroupName = "members:" + roomId };
        ctx.SseConnections.Add(conn);
        ctx.SseConnectionGroups.Add(group);
        await ctx.SaveChangesAsync(ct);
        try
        {
            await ListenAsync<MyDbContext, List<MemberInfo>>(
                getInitialData: () => MembersQuery(ctx, roomId),
                criteria: changes => changes.HasChanges<SseConnectionMetadataGroup>(),
                query: async c => await MembersQuery(c, roomId),
                ct);
        }
        finally
        {
            ctx.SseConnectionGroups.Remove(group);
            ctx.SseConnections.Remove(conn);
            await ctx.SaveChangesAsync(CancellationToken.None);
        }
    }

    [Authorize]
    [HttpGet(nameof(GetPokes))]
    [ProducesResponseType<List<Poke>>(200)]
    public Task GetPokes(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return ListenAsync<MyDbContext, List<Poke>>(
            getInitialData: () => ctx.Pokes.Where(p => p.ToUserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync(),
            criteria: changes => changes.OfType<Poke>().Any(e => e.Entity.ToUserId == userId),
            query: async c => await c.Pokes.Where(p => p.ToUserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync(),
            ct);
    }

    [Authorize]
    [HttpPost(nameof(Poke))]
    public async Task Poke(string toUserId)
    {
        var fromUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ctx.Pokes.Add(new Poke { Id = Guid.NewGuid().ToString(), FromUserId = fromUserId, ToUserId = toUserId, CreatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();
    }

    [Authorize]
    [HttpPatch(nameof(UpdateMessage))]
    public async Task UpdateMessage([FromBody] Message newMessage)
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
        ctx.Messages.Add(new Message
        {
            UserId = userId,
            Content = dto.Message,
            RoomId = dto.GroupId,
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    [Authorize]
    [HttpPost(nameof(CreateRoom))]
    public async Task CreateRoom(string name)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ctx.Rooms.Add(new Room { Id = Guid.NewGuid().ToString(), Name = name, CreatedBy = userId });
        await ctx.SaveChangesAsync();
    }

    [Authorize]
    [HttpPost(nameof(DeleteRoom))]
    public async Task DeleteRoom(string id)
    {
        var room = await ctx.Rooms.FindAsync(id);
        if (room is null) return;
        ctx.Rooms.Remove(room);
        await ctx.SaveChangesAsync();
    }

    [Authorize]
    [HttpPost(nameof(UpdateRoom))]
    public async Task UpdateRoom([FromBody] Room newRoom)
    {
        var room = await ctx.Rooms.FindAsync(newRoom.Id);
        if (room is null) return;
        ctx.Entry(room).CurrentValues.SetValues(newRoom);
        await ctx.SaveChangesAsync();
    }

    private static async Task<List<MemberInfo>> MembersQuery(MyDbContext c, string roomId) =>
        await c.SseConnectionGroups
            .Where(g => g.GroupName == "members:" + roomId)
            .Join(c.SseConnections, g => g.ConnectionId, conn => conn.ConnectionId, (g, conn) => conn)
            .GroupJoin(c.Users, conn => conn.OwnerId, u => u.Id, (conn, users) => new { conn, users })
            .SelectMany(t => t.users.DefaultIfEmpty(),
                (t, u) => new MemberInfo(t.conn.OwnerId, u != null ? u.Nickname : "Anonymous"))
            .ToListAsync();
}
