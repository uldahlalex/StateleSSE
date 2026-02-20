using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StateleSSE.AspNetCore.EfRealtime;

namespace StateleSSE.AspNetCore;

public abstract class RealtimeControllerBase(IRealtimeManager realtimeManager) : ControllerBase
{
    protected static readonly JsonSerializerOptions SseJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Opens an SSE stream. Sends <paramref name="getInitialData"/> as the first event,
    /// then streams updates whenever <paramref name="criteria"/> matches a SaveChanges on <typeparamref name="TDbContext"/>.
    /// The stream stays open until the client disconnects (<paramref name="ct"/> is cancelled).
    /// </summary>
    protected async Task ListenAsync<TDbContext, T>(
        Func<Task<T>> getInitialData,
        Func<List<EntityEntry>, bool> criteria,
        Func<TDbContext, Task<T>> query,
        CancellationToken ct = default) where TDbContext : DbContext
    {
        await using var sse = await HttpContext.OpenSseStreamAsync(cancellationToken: ct);
        var channel = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropOldest });

        var subscriptionId = realtimeManager.Subscribe<TDbContext>(
            criteria,
            async ctx => (object?)await query(ctx),
            result => channel.Writer.WriteAsync(JsonSerializer.SerializeToElement(result, SseJsonOptions), ct).AsTask());

        try
        {
            var initial = await getInitialData();
            await sse.WriteAsync(JsonSerializer.SerializeToElement(initial, SseJsonOptions), ct);
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
                await sse.WriteAsync(evt, ct);
        }
        finally
        {
            realtimeManager.Unsubscribe(subscriptionId);
            channel.Writer.TryComplete();
        }
    }

}
