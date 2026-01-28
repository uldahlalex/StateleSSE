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
    /// Write an SSE event to the stream with a named event type.
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
    /// Write an SSE event with raw string data and named event type.
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
    /// Write an SSE event without a named event type (received via onmessage).
    /// </summary>
    public async Task WriteAsync(JsonElement data, CancellationToken cancellationToken = default)
    {
        _eventId++;
        await _response.WriteAsync($"id: {_eventId}\n", cancellationToken);
        await _response.WriteAsync($"data: {data.GetRawText()}\n\n", cancellationToken);
        await _response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Write an SSE event without a named event type (received via onmessage).
    /// </summary>
    public async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        _eventId++;
        await _response.WriteAsync($"id: {_eventId}\n", cancellationToken);
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

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _keepaliveCts.Cancel();
        try { await _keepaliveTask; } catch (OperationCanceledException) { }
        _keepaliveCts.Dispose();
    }
}

/// <summary>
/// Represents a client connection to the backplane.
/// </summary>
public sealed class BackplaneConnection : IAsyncDisposable
{
    private readonly ISseBackplane _backplane;

    internal BackplaneConnection(ISseBackplane backplane, ChannelReader<SseEvent> reader, string connectionId)
    {
        _backplane = backplane;
        Reader = reader;
        ConnectionId = connectionId;
    }

    /// <summary>
    /// The unique connection ID for this client.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// The channel reader for receiving events.
    /// </summary>
    public ChannelReader<SseEvent> Reader { get; }

    /// <summary>
    /// Add this client to a group.
    /// </summary>
    public Task JoinGroupAsync(string groupName) =>
        _backplane.Groups.AddToGroupAsync(ConnectionId, groupName);

    /// <summary>
    /// Add this client to multiple groups.
    /// </summary>
    public async Task JoinGroupsAsync(IEnumerable<string> groupNames)
    {
        foreach (var groupName in groupNames)
            await _backplane.Groups.AddToGroupAsync(ConnectionId, groupName);
    }

    /// <summary>
    /// Remove this client from a group.
    /// </summary>
    public Task LeaveGroupAsync(string groupName) =>
        _backplane.Groups.RemoveFromGroupAsync(ConnectionId, groupName);

    /// <summary>
    /// Get all groups this client belongs to.
    /// </summary>
    public Task<IReadOnlyList<string>> GetGroupsAsync() =>
        _backplane.Groups.GetClientGroupsAsync(ConnectionId);

    /// <summary>
    /// Read all events from the connection.
    /// </summary>
    public IAsyncEnumerable<SseEvent> ReadAllAsync(CancellationToken cancellationToken = default)
        => Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await _backplane.DisconnectAsync(ConnectionId);
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
    /// Creates a client connection to the backplane.
    /// </summary>
    public static BackplaneConnection CreateConnection(this ISseBackplane backplane)
    {
        var (reader, connectionId) = backplane.Connect();
        return new BackplaneConnection(backplane, reader, connectionId);
    }

    /// <summary>
    /// Stream SSE events to the client with URL-based group subscriptions.
    /// Events are sent with group name as SSE event type for client-side routing.
    /// </summary>
    /// <example>
    /// <code>
    /// // Minimal API
    /// app.MapGet("/events", (HttpContext ctx, ISseBackplane bp, [FromQuery] string[] group)
    ///     => ctx.StreamSseAsync(bp, group));
    ///
    /// // Client
    /// const es = new EventSource('/events?group=chat:room1:messages&amp;group=chat:room1:typing');
    /// es.addEventListener('chat:room1:messages', e => console.log(JSON.parse(e.data)));
    /// </code>
    /// </example>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        IEnumerable<string> groups,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        await using var sse = await context.OpenSseStreamAsync(cancellationToken: cancellationToken);
        await using var connection = backplane.CreateConnection();

        await connection.JoinGroupsAsync(groups);

        await foreach (var evt in connection.ReadAllAsync(cancellationToken))
        {
            if (evt.Group != null)
            {
                await sse.WriteAsync(evt.Group, evt.Data, cancellationToken);
            }
            else
            {
                await sse.WriteAsync(evt.Data, cancellationToken);
            }
        }
    }
}
