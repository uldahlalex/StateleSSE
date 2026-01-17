using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Base controller for Server-Sent Events (SSE) with production-ready features:
/// - Automatic keepalives (30s) to prevent ANCM timeout
/// - Event IDs for client-side reconnection tracking
/// - Retry directive for automatic reconnection
/// - Type-safe event streaming
/// </summary>
public abstract class SseControllerBase(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// The SSE backplane used for pub/sub messaging.
    /// </summary>
    protected readonly ISseBackplane Backplane = backplane;

    /// <summary>
    /// Stream a specific event type to connected clients.
    /// Handles SSE protocol, keepalives, and proper cleanup automatically.
    /// </summary>
    /// <typeparam name="TEvent">The event type to stream</typeparam>
    /// <param name="channel">The Redis channel to subscribe to</param>
    /// <param name="keepaliveInterval">Keepalive interval (default: 30s)</param>
    protected async Task StreamEventType<TEvent>(
        string channel,
        TimeSpan? keepaliveInterval = null)
        where TEvent : class
    {
        var interval = keepaliveInterval ?? TimeSpan.FromSeconds(30);

        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Append("Connection", "keep-alive");
        HttpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        await HttpContext.Response.WriteAsync("retry: 3000\n\n");
        await HttpContext.Response.Body.FlushAsync();

        var (reader, subscriberId) = Backplane.Subscribe(channel);

        using var keepaliveTimer = new PeriodicTimer(interval);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);

        try
        {
            var keepaliveTask = SendKeepalives(keepaliveTimer, cts.Token);
            var streamTask = StreamEvents<TEvent>(reader, cts.Token);

            await Task.WhenAny(keepaliveTask, streamTask);
        }
        finally
        {
            cts.Cancel();
            Backplane.Unsubscribe(channel, subscriberId);
        }
    }

    /// <summary>
    /// Send periodic keepalive comments to prevent ANCM timeout (120s).
    /// Sends ": keepalive\n\n" every 30s (browsers ignore comment lines).
    /// </summary>
    private async Task SendKeepalives(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await HttpContext.Response.WriteAsync(": keepalive\n\n", cancellationToken);
                await HttpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Stream typed events from Redis backplane to client.
    /// Each event gets an incrementing ID for client-side reconnection tracking.
    /// </summary>
    private async Task StreamEvents<TEvent>(ChannelReader<object> reader, CancellationToken cancellationToken)
        where TEvent : class
    {
        var eventId = 0;

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            if (message is TEvent typedEvent)
            {
                var json = JsonSerializer.Serialize(typedEvent);
                await HttpContext.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
                await HttpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await HttpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
