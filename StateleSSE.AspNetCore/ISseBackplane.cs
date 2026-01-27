using System.Text.Json;
using System.Threading.Channels;

namespace StateleSSE.AspNetCore;

/// <summary>
/// An SSE event with optional group name and JSON payload.
/// </summary>
public readonly record struct SseEvent(string? Group, JsonElement Data);

/// <summary>
/// Event args for client disconnection.
/// </summary>
public class ClientDisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// The connection ID of the disconnected client.
    /// </summary>
    public Guid ConnectionId { get; init; }

    /// <summary>
    /// The groups the client was a member of at disconnection time.
    /// </summary>
    public IReadOnlyList<string> Groups { get; init; } = [];
}

/// <summary>
/// Backplane interface for SSE with horizontal scaling support.
/// Modeled after SignalR's Clients/Groups pattern.
/// </summary>
public interface ISseBackplane
{
    /// <summary>
    /// Access to client operations (send to specific clients).
    /// </summary>
    IBackplaneClients Clients { get; }

    /// <summary>
    /// Access to group operations (add/remove clients, query membership).
    /// </summary>
    IBackplaneGroups Groups { get; }

    /// <summary>
    /// Opens a new client connection. Returns a channel reader for receiving events and a unique connection ID.
    /// </summary>
    (ChannelReader<SseEvent> Reader, Guid ConnectionId) Connect();

    /// <summary>
    /// Close a client connection and remove from all groups.
    /// </summary>
    Task DisconnectAsync(Guid connectionId);

    /// <summary>
    /// Raised when a client disconnects. Use this to broadcast "user left" events.
    /// </summary>
    event EventHandler<ClientDisconnectedEventArgs>? OnClientDisconnected;
}

/// <summary>
/// Client targeting operations for the backplane.
/// </summary>
public interface IBackplaneClients
{
    /// <summary>
    /// Send to all connected clients across all server instances.
    /// </summary>
    Task AllAsync(object data);

    /// <summary>
    /// Send to a specific client by connection ID.
    /// </summary>
    Task ClientAsync(Guid connectionId, object data);

    /// <summary>
    /// Send to multiple specific clients by connection ID.
    /// </summary>
    Task ClientsAsync(IEnumerable<Guid> connectionIds, object data);

    /// <summary>
    /// Send to all clients in a group.
    /// </summary>
    Task GroupAsync(string groupName, object data);

    /// <summary>
    /// Send to all clients in multiple groups.
    /// </summary>
    Task GroupsAsync(IEnumerable<string> groupNames, object data);
}

/// <summary>
/// Group management operations for the backplane.
/// </summary>
public interface IBackplaneGroups
{
    /// <summary>
    /// Add a client to a group.
    /// </summary>
    Task AddToGroupAsync(Guid connectionId, string groupName);

    /// <summary>
    /// Remove a client from a group.
    /// </summary>
    Task RemoveFromGroupAsync(Guid connectionId, string groupName);

    /// <summary>
    /// Get the number of clients in a group (across all server instances).
    /// </summary>
    Task<int> GetMemberCountAsync(string groupName);

    /// <summary>
    /// Get all client connection IDs in a group (across all server instances).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetMembersAsync(string groupName);

    /// <summary>
    /// Get all groups a client belongs to.
    /// </summary>
    Task<IReadOnlyList<string>> GetClientGroupsAsync(Guid connectionId);
}
