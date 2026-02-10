using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

public class RealtimeController(ISseBackplane backplane, IRealtimeManager realtimeManager, AppDb db)
    : RealtimeControllerBase(backplane)
{
    /// <summary>
    ///Will produce the following in the browser's response tab:
    ///id: 2
    ///event: messages
    ///data: [{"id":1,"content":"hi","createdAt":"2026-02-09T10:34:37.1856196+00:00"},{"id":2,"content":"asd","createdAt":"2026-02-09T10:34:40.5670584+00:00"},{"id":3,"content":"a","createdAt":"2026-02-09T11:11:34.6666671+00:00"}]
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Since this calls .SaveChangesAsync() on dbcontext, it triggers the "Listener" to make a new query and broadcast to the group
    /// </summary>
    /// <param name="message"></param>
    [HttpPost("send")]
    public async Task Send(string message)
    {
        db.Messages.Add(new Message { Content = message });
        await db.SaveChangesAsync();
    }
}
