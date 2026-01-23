using System.Text.Json;
using System.Threading.Channels;

namespace StateleSSE.AspNetCore;

/// <summary>
/// An SSE event with channel name and JSON payload.
/// </summary>
public readonly record struct SseEvent(string Channel, JsonElement Data);

/// <summary>
/// Minimal backplane interface for SSE pub/sub with horizontal scaling support.
/// </summary>
public interface ISseBackplane
{
    /// <summary>
    /// Opens a new connection. Returns a channel reader for receiving events and a unique connection ID.
    /// </summary>
    (ChannelReader<SseEvent> Reader, Guid ConnectionId) Connect();

    /// <summary>
    /// Subscribe a connection to a channel. Events published to this channel will be delivered to the connection.
    /// </summary>
    bool Subscribe(Guid connectionId, string channel);

    /// <summary>
    /// Unsubscribe a connection from a channel.
    /// </summary>
    bool Unsubscribe(Guid connectionId, string channel);

    /// <summary>
    /// Publish an event to all subscribers of a channel (across all server instances).
    /// </summary>
    Task Publish(string channel, object data);

    /// <summary>
    /// Close a connection and remove all its subscriptions.
    /// </summary>
    void Disconnect(Guid connectionId);
}
