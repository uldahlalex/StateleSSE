using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Represents an open SSE stream to a client.
/// </summary>
public sealed class SseStream : IAsyncDisposable
{
    private readonly HttpResponse _response;
    private readonly CancellationTokenSource _keepaliveCts;
    private readonly Task _keepaliveTask;
    private int _eventId;

    internal SseStream(HttpResponse response, TimeSpan keepaliveInterval)
    {
        _response = response;
        _keepaliveCts = new CancellationTokenSource();
        _keepaliveTask = SendKeepalives(keepaliveInterval, _keepaliveCts.Token);
    }



    /// <summary>
    /// Write an SSE event without a named event type (received via onmessage).
    /// </summary>
    public async Task WriteAsync(JsonElement data, CancellationToken cancellationToken = default)
    {
        _eventId++;
        await _response.WriteAsync($"id: {_eventId}\n", cancellationToken);
        await _response.WriteAsync($"data: {data.GetRawText()}\n\n", cancellationToken);
        await _response.Body.FlushAsync(cancellationToken);
    }




    private async Task SendKeepalives(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await _response.WriteAsync(": keepalive\n\n", ct);
                await _response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _keepaliveCts.Cancel();
        try { await _keepaliveTask; } catch (OperationCanceledException) { }
        _keepaliveCts.Dispose();
    }
}

/// <summary>
/// Extension methods for SSE streaming via HttpContext.
/// </summary>
public static class SseStreamingExtensions
{
    private static readonly TimeSpan DefaultKeepalive = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Opens an SSE stream to the client. Sets appropriate headers and starts keepalives.
    /// </summary>
    public static async Task<SseStream> OpenSseStreamAsync(
        this HttpContext context,
        int retryMs = 3000,
        TimeSpan? keepaliveInterval = null,
        CancellationToken cancellationToken = default)
    {
        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";

        await response.WriteAsync($"retry: {retryMs}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);

        return new SseStream(response, keepaliveInterval ?? DefaultKeepalive);
    }
}
