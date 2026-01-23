using Microsoft.AspNetCore.Http;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for SSE streaming via HttpContext.
/// </summary>
public static class SseStreamingExtensions
{
    private static readonly TimeSpan DefaultKeepalive = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stream SSE events to the client. Sends connectionId as first "connected" event.
    /// Client uses connectionId to subscribe to channels via separate API calls.
    /// Events are sent with channel name as SSE event type for client-side routing.
    /// </summary>
    /// <example>
    /// <code>
    /// // Minimal API
    /// app.MapGet("/events", (HttpContext ctx, ISseBackplane bp) => ctx.StreamSseAsync(bp));
    /// app.MapPost("/subscribe", (SubscribeRequest req, ISseBackplane bp) =>
    ///     bp.Subscribe(req.ConnectionId, req.Channel) ? Results.Ok() : Results.NotFound());
    ///
    /// // Client
    /// const es = new EventSource('/events');
    /// es.addEventListener('connected', e => {
    ///     const { connectionId } = JSON.parse(e.data);
    ///     fetch('/subscribe', { method: 'POST', body: JSON.stringify({ connectionId, channel: 'chat:room1' }) });
    /// });
    /// es.addEventListener('chat:room1', e => console.log(JSON.parse(e.data)));
    /// </code>
    /// </example>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";

        var (reader, connectionId) = backplane.Connect();

        try
        {
            // Send retry directive and connection ID
            await response.WriteAsync("retry: 3000\n", cancellationToken);
            await response.WriteAsync("event: connected\n", cancellationToken);
            await response.WriteAsync($"data: {{\"connectionId\":\"{connectionId}\"}}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);

            // Start keepalive task
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var keepaliveTask = SendKeepalives(response, DefaultKeepalive, cts.Token);

            // Stream events with channel as SSE event type
            var eventId = 0;
            await foreach (var evt in reader.ReadAllAsync(cancellationToken))
            {
                eventId++;
                await response.WriteAsync($"id: {eventId}\n", cancellationToken);
                await response.WriteAsync($"event: {evt.Channel}\n", cancellationToken);
                await response.WriteAsync($"data: {evt.Data.GetRawText()}\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }

            cts.Cancel();
        }
        finally
        {
            backplane.Disconnect(connectionId);
        }
    }

    private static async Task SendKeepalives(HttpResponse response, TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await response.WriteAsync(": keepalive\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when connection closes
        }
    }
}
