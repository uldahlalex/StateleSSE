using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

public class ProductsController(IRealtimeManager realtimeManager, AppDb ctx)
    : RealtimeControllerBase(realtimeManager)
{
    [HttpGet(nameof(GetProducts))]
    [ProducesResponseType<List<Message>>(200)]
    public Task GetProducts(CancellationToken ct) =>
        ListenAsync<AppDb, List<Message>>(
            getInitialData:async  () => await ctx.Messages.ToListAsync(),
            criteria: changes => changes.HasChanges<Message>(),
            query: async c => await c.Messages.ToListAsync(),
            ct);
}

