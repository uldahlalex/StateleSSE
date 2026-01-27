using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StateleSSE.AspNetCore.Infrastructure;

namespace StateleSSE.AspNetCore.Tests;

public class InMemoryBackplaneTests
{
    [Fact]
    public async Task Publish_SingleSubscriber_DeliversMessage()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        await backplane.Groups.AddToGroupAsync(connectionId, "test-group");

        await backplane.Clients.GroupAsync("test-group", new { Data = "test message" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received.Group.Should().Be("test-group");
        received.Data.GetProperty("data").GetString().Should().Be("test message");

        backplane.Dispose();
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_DeliversToAll()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, conn1) = backplane.Connect();
        var (reader2, conn2) = backplane.Connect();
        await backplane.Groups.AddToGroupAsync(conn1, "test-group");
        await backplane.Groups.AddToGroupAsync(conn2, "test-group");

        await backplane.Clients.GroupAsync("test-group", new { Data = "broadcast" });

        var cts = new CancellationTokenSource(100);

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Data.GetProperty("data").GetString().Should().Be("broadcast");
        received2.Data.GetProperty("data").GetString().Should().Be("broadcast");

        backplane.Dispose();
    }

    [Fact]
    public async Task Publish_NoSubscribers_DoesNotThrow()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var act = async () => await backplane.Clients.GroupAsync("empty-group", new { Data = "test" });

        await act.Should().NotThrowAsync();

        backplane.Dispose();
    }

    [Fact]
    public async Task Disconnect_NoLongerReceivesMessages()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        await backplane.Groups.AddToGroupAsync(connectionId, "test-group");

        await backplane.DisconnectAsync(connectionId);

        await backplane.Clients.GroupAsync("test-group", new { Data = "should not receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();

        backplane.Dispose();
    }

    [Fact]
    public async Task RemoveFromGroup_NoLongerReceivesFromGroup()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        await backplane.Groups.AddToGroupAsync(connectionId, "group1");
        await backplane.Groups.AddToGroupAsync(connectionId, "group2");

        await backplane.Groups.RemoveFromGroupAsync(connectionId, "group1");

        await backplane.Clients.GroupAsync("group1", new { Data = "should not receive" });
        await backplane.Clients.GroupAsync("group2", new { Data = "should receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);
        received[0].Group.Should().Be("group2");
        received[0].Data.GetProperty("data").GetString().Should().Be("should receive");

        backplane.Dispose();
    }

    [Fact]
    public void Connect_GeneratesUniqueConnectionIds()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.Connect();
        var (_, conn2) = backplane.Connect();

        conn1.Should().NotBe(conn2);
        conn1.Should().NotBe(Guid.Empty);
        conn2.Should().NotBe(Guid.Empty);

        backplane.Dispose();
    }

    [Fact]
    public async Task GetMemberCount_ReturnsCorrectCount()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.Connect();
        var (_, conn2) = backplane.Connect();

        await backplane.Groups.AddToGroupAsync(conn1, "test-group");
        await backplane.Groups.AddToGroupAsync(conn2, "test-group");

        var count = await backplane.Groups.GetMemberCountAsync("test-group");

        count.Should().Be(2);

        backplane.Dispose();
    }

    [Fact]
    public async Task GetMembers_ReturnsAllMembers()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.Connect();
        var (_, conn2) = backplane.Connect();

        await backplane.Groups.AddToGroupAsync(conn1, "test-group");
        await backplane.Groups.AddToGroupAsync(conn2, "test-group");

        var members = await backplane.Groups.GetMembersAsync("test-group");

        members.Should().Contain(new[] { conn1, conn2 });

        backplane.Dispose();
    }

    [Fact]
    public async Task GetClientGroups_ReturnsAllGroups()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, connectionId) = backplane.Connect();

        await backplane.Groups.AddToGroupAsync(connectionId, "group1");
        await backplane.Groups.AddToGroupAsync(connectionId, "group2");

        var groups = await backplane.Groups.GetClientGroupsAsync(connectionId);

        groups.Should().Contain(new[] { "group1", "group2" });

        backplane.Dispose();
    }

    [Fact]
    public async Task Connection_SubscribedToMultipleGroups_ReceivesFromAll()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        await backplane.Groups.AddToGroupAsync(connectionId, "group1");
        await backplane.Groups.AddToGroupAsync(connectionId, "group2");

        await backplane.Clients.GroupAsync("group1", new { Data = "from group1" });
        await backplane.Clients.GroupAsync("group2", new { Data = "from group2" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(2).ToListAsync();

        received.Should().HaveCount(2);
        received.Select(e => e.Group).Should().Contain(new[] { "group1", "group2" });

        backplane.Dispose();
    }

    [Fact]
    public async Task SendToAll_DeliversToAllConnections()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, _) = backplane.Connect();
        var (reader2, _) = backplane.Connect();

        await backplane.Clients.AllAsync(new { Data = "broadcast to all" });

        var cts = new CancellationTokenSource(100);

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Data.GetProperty("data").GetString().Should().Be("broadcast to all");
        received2.Data.GetProperty("data").GetString().Should().Be("broadcast to all");
        received1.Group.Should().BeNull();
        received2.Group.Should().BeNull();

        backplane.Dispose();
    }

    [Fact]
    public async Task SendToClient_DeliversToSpecificClient()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, conn1) = backplane.Connect();
        var (reader2, _) = backplane.Connect();

        await backplane.Clients.ClientAsync(conn1, new { Data = "only for conn1" });

        var cts = new CancellationTokenSource(100);

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        received1.Data.GetProperty("data").GetString().Should().Be("only for conn1");

        // conn2 should not receive anything - verify by checking TryRead returns false
        reader2.TryRead(out _).Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public void Dispose_CompletesAllChannels()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, _) = backplane.Connect();

        backplane.Dispose();

        var cts = new CancellationTokenSource(100);
        var act = async () => await reader.ReadAllAsync(cts.Token).ToListAsync();

        act.Should().NotThrowAsync();
    }
}
