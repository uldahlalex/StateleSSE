using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

public class RealtimeController(ISseBackplane backplane, IRealtimeManager realtimeManager, AppDb db)
    : RealtimeControllerBase(backplane)
{
    [HttpGet("messages")]
    public async Task<RealtimeListenResponse<List<Message>>> GetMessages(string connectionId)
    {
        var group = "messages";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<AppDb>(connectionId, group,
            criteria: changes => changes.HasChanges<Message>(),
            query: async ctx => await ctx.Messages.OrderBy(m => m.CreatedAt).ToListAsync());

        return new RealtimeListenResponse<List<Message>>(group,
            await db.Messages.OrderBy(m => m.CreatedAt).ToListAsync());
    }

    [HttpPost("send")]
    public async Task Send(string message)
    {
        db.Messages.Add(new Message { Content = message });
        await db.SaveChangesAsync();
    }
}
