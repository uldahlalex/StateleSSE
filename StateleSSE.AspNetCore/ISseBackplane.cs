using System.Threading.Channels;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Abstraction for a Server-Sent Events (SSE) backplane.
/// Enables horizontal scaling by synchronizing events across multiple server instances.
/// Implementations can use Redis, in-memory, RabbitMQ, or any other messaging system.
/// </summary>
public interface ISseBackplane
{
    /// <summary>
    /// Opens a new connection and returns a channel reader for receiving events.
    /// The connection starts with no subscriptions - use AddSubscription to subscribe to groups.
    /// </summary>
    /// <returns>Tuple of channel reader for receiving events and unique connection ID</returns>
    (ChannelReader<object> Reader, Guid ConnectionId) OpenConnection();

    /// <summary>
    /// Adds a subscription to a group for an existing connection.
    /// Events published to this group will be forwarded to the connection's channel reader.
    /// </summary>
    /// <param name="connectionId">The connection ID from OpenConnection()</param>
    /// <param name="groupId">The group identifier to subscribe to (e.g., "chat:room1", "game:123")</param>
    /// <returns>True if subscription was added, false if connection doesn't exist or already subscribed</returns>
    bool AddSubscription(Guid connectionId, string groupId);

    /// <summary>
    /// Removes a subscription from a group for an existing connection.
    /// </summary>
    /// <param name="connectionId">The connection ID from OpenConnection()</param>
    /// <param name="groupId">The group identifier to unsubscribe from</param>
    /// <returns>True if subscription was removed, false if connection doesn't exist or wasn't subscribed</returns>
    bool RemoveSubscription(Guid connectionId, string groupId);

    /// <summary>
    /// Closes a connection and removes all its subscriptions.
    /// Should be called when the SSE connection closes.
    /// </summary>
    /// <param name="connectionId">The connection ID from OpenConnection()</param>
    void CloseConnection(Guid connectionId);

    /// <summary>
    /// Gets all group IDs that a connection is currently subscribed to.
    /// </summary>
    /// <param name="connectionId">The connection ID from OpenConnection()</param>
    /// <returns>Collection of group IDs, or empty if connection doesn't exist</returns>
    IReadOnlyCollection<string> GetSubscriptions(Guid connectionId);

    /// <summary>
    /// Publish an event to all subscribers in a specific group across all server instances.
    /// </summary>
    /// <param name="groupId">The group identifier to publish to</param>
    /// <param name="message">The event message to send</param>
    Task PublishToGroup(string groupId, object message);

    /// <summary>
    /// Publish an event to multiple groups simultaneously across all server instances.
    /// More efficient than calling PublishToGroup() multiple times.
    /// </summary>
    /// <param name="groupIds">Collection of group identifiers</param>
    /// <param name="message">The event message to send</param>
    Task PublishToGroups(IEnumerable<string> groupIds, object message);

    /// <summary>
    /// Broadcast an event to all connections across all server instances.
    /// </summary>
    /// <param name="message">The event message to broadcast</param>
    Task PublishToAll(object message);

    /// <summary>
    /// Get the number of local connections subscribed to a specific group on this server instance.
    /// </summary>
    /// <param name="groupId">The group identifier</param>
    /// <returns>Number of local connections subscribed to this group</returns>
    int GetLocalSubscriberCount(string groupId);

    /// <summary>
    /// Get all group IDs that have active subscribers on this server instance.
    /// </summary>
    /// <returns>Collection of group IDs</returns>
    IEnumerable<string> GetLocalGroups();

    /// <summary>
    /// Get diagnostic information about the backplane state on this server instance.
    /// Useful for monitoring and debugging.
    /// </summary>
    /// <returns>Diagnostic information</returns>
    BackplaneDiagnostics GetDiagnostics();
}

/// <summary>
/// Diagnostic information about backplane state on a server instance.
/// </summary>
public record BackplaneDiagnostics
{
    /// <summary>
    /// Total number of active connections on this server
    /// </summary>
    public required int TotalConnections { get; init; }

    /// <summary>
    /// Total number of groups with active subscribers on this server
    /// </summary>
    public required int TotalGroups { get; init; }

    /// <summary>
    /// Total number of subscriptions across all connections on this server
    /// </summary>
    public required int TotalSubscriptions { get; init; }

    /// <summary>
    /// Detailed information about each group
    /// </summary>
    public required GroupInfo[] Groups { get; init; }

    /// <summary>
    /// Detailed information about each connection
    /// </summary>
    public required ConnectionInfo[] Connections { get; init; }
}

/// <summary>
/// Information about a specific group on this server instance.
/// </summary>
public record GroupInfo
{
    /// <summary>
    /// The group identifier
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Number of local connections subscribed to this group on this server
    /// </summary>
    public required int SubscriberCount { get; init; }
}

/// <summary>
/// Information about a specific connection on this server instance.
/// </summary>
public record ConnectionInfo
{
    /// <summary>
    /// The connection identifier
    /// </summary>
    public required Guid ConnectionId { get; init; }

    /// <summary>
    /// Number of groups this connection is subscribed to
    /// </summary>
    public required int SubscriptionCount { get; init; }

    /// <summary>
    /// List of group IDs this connection is subscribed to
    /// </summary>
    public required string[] SubscribedGroups { get; init; }
}
