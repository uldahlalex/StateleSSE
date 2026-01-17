using Microsoft.AspNetCore.Mvc;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Base controller for Server-Sent Events (SSE).
/// Provides convenient access to production-ready SSE streaming via HttpContext extensions.
/// </summary>
public abstract class SseControllerBase(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// The SSE backplane used for pub/sub messaging.
    /// </summary>
    protected readonly ISseBackplane Backplane = backplane;

    /// <summary>
    /// Stream a specific event type to connected clients.
    /// Includes keepalives, event IDs, retry directive, and proper cleanup.
    /// </summary>
    /// <typeparam name="TEvent">The event type to stream</typeparam>
    /// <param name="channel">The channel to subscribe to</param>
    /// <param name="keepaliveInterval">Keepalive interval (default: 30s)</param>
    protected async Task StreamEventType<TEvent>(
        string channel,
        TimeSpan? keepaliveInterval = null)
        where TEvent : class
    {
        await HttpContext.StreamSseAsync<TEvent>(Backplane, channel, keepaliveInterval);
    }
}
