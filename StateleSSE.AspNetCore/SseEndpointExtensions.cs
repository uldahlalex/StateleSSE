using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for registering SSE endpoints with Minimal APIs.
/// </summary>
public static class SseEndpointExtensions
{
    /// <summary>
    /// Maps a GET endpoint for Server-Sent Events streaming.
    /// The path must contain "Stream" for CodeGen discovery.
    /// </summary>
    /// <typeparam name="TEvent">The event type to stream</typeparam>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="pattern">The route pattern (must contain "Stream")</param>
    /// <param name="handler">The request handler</param>
    /// <returns>Route handler builder for further configuration</returns>
    public static RouteHandlerBuilder MapSseGet<TEvent>(
        this IEndpointRouteBuilder endpoints,
        [SseEndpoint] string pattern,
        Delegate handler) where TEvent : class
    {
        return endpoints.MapGet(pattern, handler)
            .Produces<TEvent>(200, "text/event-stream");
    }
}
