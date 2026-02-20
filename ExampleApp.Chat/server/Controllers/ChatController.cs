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

public class ChatController(
    ISseBackplane backplane,
    IRealtimeManager realtimeManager,
    JwtService jwtService,
    MyDbContext ctx) : RealtimeControllerBase(backplane, realtimeManager)
{
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
    public Task GetMessages(string roomId, CancellationToken ct) =>
        ListenAsync<MyDbContext, List<Message>>(
            group: "message:" + roomId,
            getInitialData: () => ctx.Messages.Where(m => m.RoomId == roomId).OrderByDescending(m => m.CreatedAt).ToListAsync(),
            criteria: changes => changes.HasAdded<Message>(),
            query: async c => await c.Messages.Where(m => m.RoomId == roomId).OrderByDescending(m => m.CreatedAt).ToListAsync(),
            ct);

    [HttpGet(nameof(GetRooms))]
    public Task GetRooms(CancellationToken ct) =>
        ListenAsync<MyDbContext, List<Room>>(
            group: "rooms",
            getInitialData: () => ctx.Rooms.ToListAsync(),
            criteria: changes => changes.HasChanges<Room>(),
            query: async c => await c.Rooms.ToListAsync(),
            ct);

    [HttpGet(nameof(GetMembers))]
    public Task GetMembers(string roomId, CancellationToken ct)
    {
        var group = "members:" + roomId;
        return ListenAsync<MyDbContext, List<MemberInfo>>(
            group: group,
            getInitialData: () => MembersQuery(ctx, group),
            criteria: changes => changes.HasChanges<SseConnectionGroup>(),
            query: async c => await MembersQuery(c, group),
            ct);
    }

    [HttpGet(nameof(GetPokes))]
    public async Task GetPokes(CancellationToken ct)
    {
        await using var sse = await HttpContext.OpenSseStreamAsync(cancellationToken: ct);
        await using var conn = backplane.CreateConnection();
        await conn.JoinGroupAsync($"poke:{conn.ConnectionId}");
        await sse.WriteAsync(JsonSerializer.SerializeToElement(new { connectionId = conn.ConnectionId }, SseJsonOptions), ct);
        await foreach (var evt in conn.ReadAllAsync(ct))
            await sse.WriteAsync(evt.Data, ct);
    }

    [HttpPost(nameof(Poke))]
    public Task Poke(string connectionId) =>
        backplane.Clients.SendToGroupAsync($"poke:{connectionId}", new { message = "You have been poked" });

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

    private static async Task<List<MemberInfo>> MembersQuery(MyDbContext c, string group) =>
        await c.SseConnectionGroups
            .Where(g => g.GroupName == group)
            .Join(c.SseConnections, g => g.ConnectionId, conn => conn.ConnectionId, (g, conn) => new { g, conn })
            .GroupJoin(c.Users, t => t.conn.OwnerId, u => u.Id, (t, users) => new { t, users })
            .SelectMany(t => t.users.DefaultIfEmpty(),
                (t, u) => new MemberInfo(t.t.g.ConnectionId, u != null ? u.Nickname : "Anonymous"))
            .ToListAsync();
}
