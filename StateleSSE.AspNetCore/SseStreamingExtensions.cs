using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Threading.Channels;

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
    /// Write an SSE event to the stream.
    /// </summary>
    public async Task WriteAsync(string eventType, JsonElement data, CancellationToken cancellationToken = default)
    {
        _eventId++;
        await _response.WriteAsync($"id: {_eventId}\n", cancellationToken);
        await _response.WriteAsync($"event: {eventType}\n", cancellationToken);
        await _response.WriteAsync($"data: {data.GetRawText()}\n\n", cancellationToken);
        await _response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Write an SSE event with raw string data.
    /// </summary>
    public async Task WriteAsync(string eventType, string data, CancellationToken cancellationToken = default)
    {
        _eventId++;
        await _response.WriteAsync($"id: {_eventId}\n", cancellationToken);
        await _response.WriteAsync($"event: {eventType}\n", cancellationToken);
        await _response.WriteAsync($"data: {data}\n\n", cancellationToken);
        await _response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Write an SSE comment (useful for custom keepalives or debugging).
    /// </summary>
    public async Task WriteCommentAsync(string comment, CancellationToken cancellationToken = default)
    {
        await _response.WriteAsync($": {comment}\n\n", cancellationToken);
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

    public async ValueTask DisposeAsync()
    {
        _keepaliveCts.Cancel();
        try { await _keepaliveTask; } catch (OperationCanceledException) { }
        _keepaliveCts.Dispose();
    }
}

/// <summary>
/// Represents a connection to the backplane with subscriptions.
/// </summary>
public sealed class BackplaneConnection : IDisposable
{
    private readonly ISseBackplane _backplane;

    internal BackplaneConnection(ISseBackplane backplane, ChannelReader<SseEvent> reader, Guid connectionId)
    {
        _backplane = backplane;
        Reader = reader;
        ConnectionId = connectionId;
    }

    public Guid ConnectionId { get; }
    public ChannelReader<SseEvent> Reader { get; }

    public void Subscribe(string channel) => _backplane.Subscribe(ConnectionId, channel);

    public void Subscribe(IEnumerable<string> channels)
    {
        foreach (var channel in channels)
            _backplane.Subscribe(ConnectionId, channel);
    }

    public void Unsubscribe(string channel) => _backplane.Unsubscribe(ConnectionId, channel);

    public IAsyncEnumerable<SseEvent> ReadAllAsync(CancellationToken cancellationToken = default)
        => Reader.ReadAllAsync(cancellationToken);

    public void Dispose() => _backplane.Disconnect(ConnectionId);
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

    /// <summary>
    /// Creates a connection to the backplane.
    /// </summary>
    public static BackplaneConnection CreateConnection(this ISseBackplane backplane)
    {
        var (reader, connectionId) = backplane.Connect();
        return new BackplaneConnection(backplane, reader, connectionId);
    }

    /// <summary>
    /// Stream SSE events to the client with URL-based channel subscriptions.
    /// Events are sent with channel name as SSE event type for client-side routing.
    /// </summary>
    /// <example>
    /// <code>
    /// // Minimal API
    /// app.MapGet("/events", (HttpContext ctx, ISseBackplane bp, [FromQuery] string[] channel)
    ///     => ctx.StreamSseAsync(bp, channel));
    ///
    /// // Client
    /// const es = new EventSource('/events?channel=chat:room1:messages&amp;channel=chat:room1:typing');
    /// es.addEventListener('chat:room1:messages', e => console.log(JSON.parse(e.data)));
    /// </code>
    /// </example>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        IEnumerable<string> channels,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";

        var (reader, connectionId) = backplane.Connect();

        // Subscribe to all requested channels
        foreach (var channel in channels)
        {
            backplane.Subscribe(connectionId, channel);
        }

        try
        {
            // Send retry directive
            await response.WriteAsync("retry: 3000\n\n", cancellationToken);
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
