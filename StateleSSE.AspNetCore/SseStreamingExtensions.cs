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
    /// Opens an SSE connection without any initial subscriptions.
    /// The connection ID is sent as the first event, which the client uses for subscription requests.
    /// Use backplane.AddSubscription/RemoveSubscription to dynamically manage subscriptions.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation.</param>
    /// <param name="keepaliveInterval">Keepalive interval (default: 30s to prevent ANCM 120s timeout).</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    /// <example>
    /// <code>
    /// // Stream endpoint - opens connection
    /// [HttpGet("stream")]
    /// public async Task Stream()
    /// {
    ///     await HttpContext.StreamSseAsync(backplane);
    /// }
    ///
    /// // Subscribe endpoint - adds subscription to existing connection
    /// [HttpPost("subscribe")]
    /// public IActionResult Subscribe([FromBody] SubscribeRequest request)
    /// {
    ///     var success = backplane.AddSubscription(request.ConnectionId, request.Channel);
    ///     return success ? Ok() : NotFound();
    /// }
    /// </code>
    /// </example>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        TimeSpan? keepaliveInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = keepaliveInterval ?? TimeSpan.FromSeconds(30);
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        var (reader, connectionId) = backplane.OpenConnection();

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        // Send retry directive
        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);

        // Send connection ID as the first event
        await context.Response.WriteAsync("event: connected\n", cancellationToken);
        await context.Response.WriteAsync($"data: {{\"connectionId\":\"{connectionId}\"}}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

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
            backplane.CloseConnection(connectionId);
        }
    }

    /// <summary>
    /// Opens an SSE connection and immediately subscribes to a single channel.
    /// Events are streamed with the event type name.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to stream. Must be a class.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation.</param>
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

        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, channel);

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

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
            backplane.CloseConnection(connectionId);
        }
    }

    /// <summary>
    /// Opens an SSE connection and immediately subscribes to a single channel.
    /// All messages are serialized as received without type filtering.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation.</param>
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

        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, channel);

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

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
            backplane.CloseConnection(connectionId);
        }
    }

    /// <summary>
    /// Opens an SSE connection and immediately subscribes to a single channel.
    /// Streams multiple event types on a single SSE connection with named events.
    /// Each event type is sent with its type name as the SSE event field.
    /// Client can subscribe to specific event types using EventSource.addEventListener().
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation.</param>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="eventTypes">The event types to stream. Must all be classes.</param>
    /// <param name="keepaliveInterval">Keepalive interval (default: 30s).</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        Type[] eventTypes,
        TimeSpan? keepaliveInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = keepaliveInterval ?? TimeSpan.FromSeconds(30);
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, channel);

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        using var keepaliveTimer = new PeriodicTimer(interval);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var keepaliveTask = SendKeepalives(context, keepaliveTimer, cts.Token);
            var streamTask = StreamMultipleEventTypes(context, reader, eventTypes, cts.Token);

            await Task.WhenAny(keepaliveTask, streamTask);
        }
        finally
        {
            cts.Cancel();
            backplane.CloseConnection(connectionId);
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
        var eventName = typeof(TEvent).Name;

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            if (message is TEvent typedEvent)
            {
                var json = JsonSerializer.Serialize(typedEvent);
                await context.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
                await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
            else if (message is JsonElement jsonElement)
            {
                try
                {
                    var deserializedEvent = jsonElement.Deserialize<TEvent>();
                    if (deserializedEvent != null)
                    {
                        var json = JsonSerializer.Serialize(deserializedEvent);
                        await context.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
                        await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken);
                        await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                        await context.Response.Body.FlushAsync(cancellationToken);
                    }
                }
                catch
                {
                }
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

    private static async Task StreamMultipleEventTypes(HttpContext context, ChannelReader<object> reader, Type[] eventTypes, CancellationToken cancellationToken)
    {
        var eventId = 0;
        var typeMap = eventTypes.ToDictionary(t => t, t => t.Name);

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            var messageType = message.GetType();

            if (message is JsonElement jsonElement)
            {
                foreach (var eventType in eventTypes)
                {
                    try
                    {
                        var deserializedEvent = jsonElement.Deserialize(eventType);
                        if (deserializedEvent != null)
                        {
                            var json = JsonSerializer.Serialize(deserializedEvent);
                            var eventName = typeMap[eventType];
                            await context.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
                            await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken);
                            await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                            await context.Response.Body.FlushAsync(cancellationToken);
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            else if (typeMap.TryGetValue(messageType, out var eventName))
            {
                var json = JsonSerializer.Serialize(message);
                await context.Response.WriteAsync($"id: {++eventId}\n", cancellationToken);
                await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
