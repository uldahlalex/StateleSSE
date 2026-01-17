using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for Server-Sent Events (SSE) streaming with any ISseBackplane implementation.
/// Works with both controllers and Minimal APIs.
/// </summary>
public static class SseStreamingExtensions
{
    /// <summary>
    /// Streams Server-Sent Events with production features:
    /// - Automatic keepalives (prevents ANCM timeout)
    /// - Event IDs for reconnection tracking
    /// - Retry directive for automatic reconnection
    /// - Type-safe event streaming
    /// </summary>
    /// <typeparam name="TEvent">The type of events to stream. Must be a class.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to (e.g., "game:123:PlayerJoinedEvent").</param>
    /// <param name="keepaliveInterval">Keepalive interval (default: 30s to prevent ANCM 120s timeout).</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync<TEvent>(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        TimeSpan? keepaliveInterval = null,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        var interval = keepaliveInterval ?? TimeSpan.FromSeconds(30);
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        var (reader, subscriberId) = backplane.Subscribe(channel);

        using var keepaliveTimer = new PeriodicTimer(interval);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var keepaliveTask = SendKeepalives(context, keepaliveTimer, cts.Token);
            var streamTask = StreamEvents<TEvent>(context, reader, cts.Token);

            await Task.WhenAny(keepaliveTask, streamTask);
        }
        finally
        {
            cts.Cancel();
            backplane.Unsubscribe(channel, subscriberId);
        }
    }

    /// <summary>
    /// Streams untyped Server-Sent Events with production features.
    /// All messages are serialized as received without type filtering.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="keepaliveInterval">Keepalive interval (default: 30s).</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        TimeSpan? keepaliveInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = keepaliveInterval ?? TimeSpan.FromSeconds(30);
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        var (reader, subscriberId) = backplane.Subscribe(channel);

        using var keepaliveTimer = new PeriodicTimer(interval);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var keepaliveTask = SendKeepalives(context, keepaliveTimer, cts.Token);
            var streamTask = StreamEventsUntyped(context, reader, cts.Token);

            await Task.WhenAny(keepaliveTask, streamTask);
        }
        finally
        {
            cts.Cancel();
            backplane.Unsubscribe(channel, subscriberId);
        }
    }

    private static async Task SendKeepalives(HttpContext context, PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await context.Response.WriteAsync(": keepalive\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task StreamEvents<TEvent>(HttpContext context, ChannelReader<object> reader, CancellationToken cancellationToken)
        where TEvent : class
    {
        var eventId = 0;

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            if (message is TEvent typedEvent)
            {
                var json = JsonSerializer.Serialize(typedEvent);
                await context.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
    }

    private static async Task StreamEventsUntyped(HttpContext context, ChannelReader<object> reader, CancellationToken cancellationToken)
    {
        var eventId = 0;

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            var json = JsonSerializer.Serialize(message);
            await context.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
            await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
}
