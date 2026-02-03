using StateleSSE.AspNetCore;

public class Example(ISseBackplane backplane)
{
    public async Task Send(string connectionId, object data, string id1 = "abc", string id2 = "xyz")
    {
        // Broadcast to all
        await backplane.Clients.SendToAllAsync(data);

        // Send to group
        await backplane.Clients.SendToGroupAsync("room-1", data);

        // Send to multiple groups
        await backplane.Clients.SendToGroupsAsync(["room-1", "room-2"], data);

        // Send to specific client
        await backplane.Clients.SendToClientAsync(connectionId, data);

        // Send to multiple clients
        await backplane.Clients.SendToClientsAsync([id1, id2], data);
    }

    public async Task Group(string connectionId)
    {
        // Add/remove from group
        await backplane.Groups.AddToGroupAsync(connectionId, "room-1");
        await backplane.Groups.RemoveFromGroupAsync(connectionId, "room-1");

        // Query membership
        var members = await backplane.Groups.GetMembersAsync("room-1");
        var count = await backplane.Groups.GetMemberCountAsync("room-1");
        var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);
    }
}