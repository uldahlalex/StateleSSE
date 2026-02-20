using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

[ApiController]
[Route("[controller]/[action]")]
public class ChatController(IRealtimeManager realtimeManager, AppDb ctx) : RealtimeControllerBase(realtimeManager)
{
    [HttpPost]
    public async Task<User> Login(string name)
    {
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Name == name);
        if (user is null)
        {
            user = new User { Name = name };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
        }
        return user;
    }

    [HttpGet]
    [ProducesResponseType<List<Message>>(200)]
    public Task GetMessages(string roomId, CancellationToken ct) =>
        ListenAsync<AppDb, List<Message>>(
            getInitialData: () => ctx.Messages.Where(m => m.RoomId == roomId).OrderBy(m => m.CreatedAt).ToListAsync(),
            criteria: changes => changes.Any(c => c.Entity is Message),
            query: async c => await c.Messages.Where(m => m.RoomId == roomId).OrderBy(m => m.CreatedAt).ToListAsync(),
            ct);

    [HttpGet]
    [ProducesResponseType<List<User>>(200)]
    public async Task GetMembers(string roomId, string? userId, CancellationToken ct)
    {
        var conn  = new SseConnectionMetadata      { ConnectionId = Guid.NewGuid().ToString(),  OwnerId = userId };
        var group = new SseConnectionMetadataGroup { ConnectionId = conn.ConnectionId, GroupName = "room:" + roomId };
        ctx.Connections.Add(conn);
        ctx.ConnectionGroups.Add(group);
        await ctx.SaveChangesAsync(ct);
        try
        {
            await ListenAsync<AppDb, List<User>>(
                getInitialData: () => MembersQuery(ctx, roomId),
                criteria: changes => changes.Any(c => c.Entity is SseConnectionMetadataGroup),
                query: async c => await MembersQuery(c, roomId),
                ct);
            
        }
        finally
        {
            ctx.ConnectionGroups.Remove(group);
            ctx.Connections.Remove(conn);
            await ctx.SaveChangesAsync(CancellationToken.None);
        }
    }

    [HttpPost]
    public async Task PostMessage(string roomId, string content, string? userId)
    {
        ctx.Messages.Add(new Message { RoomId = roomId, Content = content, UserId = userId });
        await ctx.SaveChangesAsync();
    }

    private static Task<List<User>> MembersQuery(AppDb db, string roomId) =>
        db.ConnectionGroups
            .Where(g => g.GroupName == "room:" + roomId)
            .Join(db.Connections, g => g.ConnectionId, c => c.ConnectionId, (_, c) => c)
            .Join(db.Users, c => c.OwnerId, u => u.Id, (_, u) => u)
            .ToListAsync();
}
