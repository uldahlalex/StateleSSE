using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;

namespace ExampleApp.Chat.Controllers;

[ApiController]
[Route("api/realtime")]
public class RealtimeController(ISseBackplane backplane) : ControllerBase
{
    /// <summary>
    /// Open SSE stream. Returns connectionId immediately. Client then joins groups via API.
    /// </summary>
    [HttpGet("connect")]
    [Produces("text/event-stream")]
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync();
        await using var connection = backplane.CreateConnection();

        // Send connectionId immediately - client needs this to join groups
        await sse.WriteAsync("connected", JsonSerializer.Serialize(new
        {
            connectionId = connection.ConnectionId
        }));

        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }

    /// <summary>
    /// Join a group. Optionally auto-broadcasts presence to the group.
    /// </summary>
    [HttpPost("groups/join")]
    public async Task<ActionResult<JoinGroupResponse>> JoinGroup([FromBody] JoinGroupRequest request)
    {
        await backplane.Groups.AddToGroupAsync(request.ConnectionId, request.Group);

        var members = await backplane.Groups.GetMembersAsync(request.Group);

        // Auto-broadcast presence update to the group
        await backplane.Clients.GroupAsync(request.Group, new PresenceUpdate
        {
            Type = "join",
            Group = request.Group,
            ConnectionId = request.ConnectionId,
            MemberCount = members.Count
        });

        return Ok(new JoinGroupResponse
        {
            Group = request.Group,
            MemberCount = members.Count
        });
    }

    /// <summary>
    /// Leave a group. Auto-broadcasts presence update.
    /// </summary>
    [HttpPost("groups/leave")]
    public async Task<ActionResult<LeaveGroupResponse>> LeaveGroup([FromBody] LeaveGroupRequest request)
    {
        await backplane.Groups.RemoveFromGroupAsync(request.ConnectionId, request.Group);

        var members = await backplane.Groups.GetMembersAsync(request.Group);

        await backplane.Clients.GroupAsync(request.Group, new PresenceUpdate
        {
            Type = "leave",
            Group = request.Group,
            ConnectionId = request.ConnectionId,
            MemberCount = members.Count
        });

        return Ok(new LeaveGroupResponse
        {
            Group = request.Group,
            MemberCount = members.Count
        });
    }

    /// <summary>
    /// Get current members of a group.
    /// </summary>
    [HttpGet("groups/{group}/members")]
    public async Task<ActionResult<GroupMembersResponse>> GetGroupMembers(string group)
    {
        var members = await backplane.Groups.GetMembersAsync(group);
        return Ok(new GroupMembersResponse
        {
            Group = group,
            Members = members.ToList(),
            MemberCount = members.Count
        });
    }

    /// <summary>
    /// Send message to a group.
    /// </summary>
    [HttpPost("groups/send")]
    public async Task<IActionResult> SendToGroup([FromBody] SendToGroupRequest request)
    {
        await backplane.Clients.GroupAsync(request.Group, new GroupMessage
        {
            From = request.ConnectionId,
            Group = request.Group,
            Payload = request.Payload,
            Timestamp = DateTimeOffset.UtcNow
        });
        return Ok();
    }

    /// <summary>
    /// Send message to a specific client.
    /// </summary>
    [HttpPost("clients/send")]
    public async Task<IActionResult> SendToClient([FromBody] SendToClientRequest request)
    {
        await backplane.Clients.ClientAsync(request.TargetConnectionId, new DirectMessage
        {
            From = request.FromConnectionId,
            Payload = request.Payload,
            Timestamp = DateTimeOffset.UtcNow
        });
        return Ok();
    }

    /// <summary>
    /// Broadcast to all connected clients.
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest request)
    {
        await backplane.Clients.AllAsync(new BroadcastMessage
        {
            Payload = request.Payload,
            Timestamp = DateTimeOffset.UtcNow
        });
        return Ok();
    }

    /// <summary>
    /// Get all groups a connection belongs to.
    /// </summary>
    [HttpGet("connections/{connectionId}/groups")]
    public async Task<ActionResult<ConnectionGroupsResponse>> GetConnectionGroups(Guid connectionId)
    {
        var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);
        return Ok(new ConnectionGroupsResponse
        {
            ConnectionId = connectionId,
            Groups = groups.ToList()
        });
    }
}

// Requests
public record JoinGroupRequest(Guid ConnectionId, string Group);
public record LeaveGroupRequest(Guid ConnectionId, string Group);
public record SendToGroupRequest(Guid ConnectionId, string Group, object Payload);
public record SendToClientRequest(Guid FromConnectionId, Guid TargetConnectionId, object Payload);
public record BroadcastRequest(object Payload);

// Responses
public record JoinGroupResponse { public required string Group { get; init; } public required int MemberCount { get; init; } }
public record LeaveGroupResponse { public required string Group { get; init; } public required int MemberCount { get; init; } }
public record GroupMembersResponse { public required string Group { get; init; } public required List<Guid> Members { get; init; } public required int MemberCount { get; init; } }
public record ConnectionGroupsResponse { public required Guid ConnectionId { get; init; } public required List<string> Groups { get; init; } }

// Events (sent via SSE)
public class PresenceUpdate
{
    public required string Type { get; set; }  // "join" | "leave"
    public required string Group { get; set; }
    public required Guid ConnectionId { get; set; }
    public required int MemberCount { get; set; }
}

public class GroupMessage
{
    public required Guid From { get; set; }
    public required string Group { get; set; }
    public required object Payload { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
}

public class DirectMessage
{
    public required Guid From { get; set; }
    public required object Payload { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
}

public class BroadcastMessage
{
    public required object Payload { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
}
