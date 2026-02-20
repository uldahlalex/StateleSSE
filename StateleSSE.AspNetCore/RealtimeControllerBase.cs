using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore.EfRealtime;

namespace StateleSSE.AspNetCore;

public abstract class RealtimeControllerBase(ISseBackplane backplane, IRealtimeManager realtimeManager) : ControllerBase
{
    protected static readonly JsonSerializerOptions SseJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Opens an SSE stream for this endpoint. Sends <paramref name="getInitialData"/> as the first event,
    /// then streams updates whenever <paramref name="criteria"/> matches a SaveChanges on <typeparamref name="TDbContext"/>.
    /// The stream stays open until the client disconnects (<paramref name="ct"/> is cancelled).
    /// </summary>
    protected async Task ListenAsync<TDbContext, T>(
        string group,
        Func<Task<T>> getInitialData,
        Func<ChangeSnapshot, bool> criteria,
        Func<TDbContext, Task<T>> query,
        CancellationToken ct = default) where TDbContext : DbContext
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var sse = await HttpContext.OpenSseStreamAsync(cancellationToken: ct);
        await using var conn = backplane.CreateConnection(ownerId);
        await conn.JoinGroupAsync(group);
        await realtimeManager.SubscribeAsync<TDbContext>(conn.ConnectionId, group, criteria,
            async ctx => (object?)await query((TDbContext)ctx));
        var initial = await getInitialData();
        await sse.WriteAsync(JsonSerializer.SerializeToElement(initial, SseJsonOptions), ct);
        await foreach (var evt in conn.ReadAllAsync(ct))
            await sse.WriteAsync(evt.Data, ct);
    }
}
