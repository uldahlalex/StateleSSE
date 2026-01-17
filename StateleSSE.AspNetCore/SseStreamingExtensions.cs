using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for simplifying Server-Sent Events (SSE) streaming with any ISseBackplane implementation.
/// Eliminates boilerplate for headers, subscription management, and cleanup.
/// </summary>
public static class SseStreamingExtensions
{
    /// <summary>
    /// Streams Server-Sent Events from a backplane channel to the HTTP response.
    /// Automatically handles SSE headers, subscription lifecycle, and proper cleanup.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to stream. Must be a class.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to (e.g., "game:123:PlayerJoinedEvent").</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync<TEvent>(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        var (reader, subscriberId) = backplane.Subscribe(channel);

        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken))
            {
                if (message is TEvent typedEvent)
                {
                    var json = JsonSerializer.Serialize(typedEvent);
                    await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        finally
        {
            backplane.Unsubscribe(channel, subscriberId);
        }
    }

    /// <summary>
    /// Streams untyped Server-Sent Events from a backplane channel.
    /// All messages are serialized as received without type filtering.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        var (reader, subscriberId) = backplane.Subscribe(channel);

        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(message);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            backplane.Unsubscribe(channel, subscriberId);
        }
    }
}
