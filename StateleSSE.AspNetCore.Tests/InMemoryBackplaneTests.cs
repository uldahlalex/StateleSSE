using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StateleSSE.AspNetCore.Infrastructure;

namespace StateleSSE.AspNetCore.Tests;

public class InMemoryBackplaneTests
{
    [Fact]
    public async Task PublishToGroup_SingleSubscriber_DeliversMessage()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, "test-group");

        var message = new { Data = "test message" };
        await backplane.PublishToGroup("test-group", message);

        var receivedMessages = new List<object>();
        await foreach (var msg in reader.ReadAllAsync(new CancellationTokenSource(100).Token))
        {
            receivedMessages.Add(msg);
            break;
        }

        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Should().BeEquivalentTo(message);

        backplane.Dispose();
    }

    [Fact]
    public async Task PublishToGroup_MultipleSubscribers_DeliversToAll()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, conn1) = backplane.OpenConnection();
        var (reader2, conn2) = backplane.OpenConnection();
        backplane.AddSubscription(conn1, "test-group");
        backplane.AddSubscription(conn2, "test-group");

        var message = new { Data = "broadcast" };
        await backplane.PublishToGroup("test-group", message);

        var cts = new CancellationTokenSource(100);

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Should().BeEquivalentTo(message);
        received2.Should().BeEquivalentTo(message);

        backplane.Dispose();
    }

    [Fact]
    public async Task PublishToGroup_NoSubscribers_DoesNotThrow()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var act = async () => await backplane.PublishToGroup("empty-group", new { Data = "test" });

        await act.Should().NotThrowAsync();

        backplane.Dispose();
    }

    [Fact]
    public async Task CloseConnection_NoLongerReceivesMessages()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, "test-group");

        backplane.CloseConnection(connectionId);

        await backplane.PublishToGroup("test-group", new { Data = "should not receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();

        backplane.Dispose();
    }

    [Fact]
    public async Task RemoveSubscription_NoLongerReceivesFromGroup()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, "group1");
        backplane.AddSubscription(connectionId, "group2");

        backplane.RemoveSubscription(connectionId, "group1");

        await backplane.PublishToGroup("group1", new { Data = "should not receive" });
        await backplane.PublishToGroup("group2", new { Data = "should receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);
        received[0].Should().BeEquivalentTo(new { Data = "should receive" });

        backplane.Dispose();
    }

    [Fact]
    public async Task PublishToAll_DeliversToAllConnections()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, conn1) = backplane.OpenConnection();
        var (reader2, conn2) = backplane.OpenConnection();
        backplane.AddSubscription(conn1, "group1");
        backplane.AddSubscription(conn2, "group2");

        var message = new { Data = "broadcast to all" };
        await backplane.PublishToAll(message);

        var cts = new CancellationTokenSource(100);

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Should().BeEquivalentTo(message);
        received2.Should().BeEquivalentTo(message);

        backplane.Dispose();
    }

    [Fact]
    public void GetLocalSubscriberCount_ReturnsCorrectCount()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.OpenConnection();
        var (_, conn2) = backplane.OpenConnection();
        backplane.AddSubscription(conn1, "test-group");
        backplane.AddSubscription(conn2, "test-group");

        var count = backplane.GetLocalSubscriberCount("test-group");

        count.Should().Be(2);

        backplane.Dispose();
    }

    [Fact]
    public void GetLocalGroups_ReturnsAllGroupIds()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.OpenConnection();
        var (_, conn2) = backplane.OpenConnection();
        var (_, conn3) = backplane.OpenConnection();
        backplane.AddSubscription(conn1, "group1");
        backplane.AddSubscription(conn2, "group2");
        backplane.AddSubscription(conn3, "group3");

        var groups = backplane.GetLocalGroups().ToList();

        groups.Should().HaveCount(3);
        groups.Should().Contain(new[] { "group1", "group2", "group3" });

        backplane.Dispose();
    }

    [Fact]
    public void GetDiagnostics_ReturnsCorrectStatistics()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.OpenConnection();
        var (_, conn2) = backplane.OpenConnection();
        var (_, conn3) = backplane.OpenConnection();
        backplane.AddSubscription(conn1, "group1");
        backplane.AddSubscription(conn2, "group1");
        backplane.AddSubscription(conn3, "group2");

        var diagnostics = backplane.GetDiagnostics();

        diagnostics.TotalConnections.Should().Be(3);
        diagnostics.TotalGroups.Should().Be(2);
        diagnostics.TotalSubscriptions.Should().Be(3);
        diagnostics.Groups.Should().HaveCount(2);
        diagnostics.Connections.Should().HaveCount(3);

        backplane.Dispose();
    }

    [Fact]
    public void OpenConnection_GeneratesUniqueConnectionIds()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, conn1) = backplane.OpenConnection();
        var (_, conn2) = backplane.OpenConnection();

        conn1.Should().NotBe(conn2);
        conn1.Should().NotBe(Guid.Empty);
        conn2.Should().NotBe(Guid.Empty);

        backplane.Dispose();
    }

    [Fact]
    public void AddSubscription_ReturnsFalseForInvalidConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var result = backplane.AddSubscription(Guid.NewGuid(), "test-group");

        result.Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public void AddSubscription_ReturnsFalseIfAlreadySubscribed()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, connectionId) = backplane.OpenConnection();

        var first = backplane.AddSubscription(connectionId, "test-group");
        var second = backplane.AddSubscription(connectionId, "test-group");

        first.Should().BeTrue();
        second.Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public void RemoveSubscription_ReturnsFalseForInvalidConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var result = backplane.RemoveSubscription(Guid.NewGuid(), "test-group");

        result.Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public void GetSubscriptions_ReturnsAllGroupsForConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, "group1");
        backplane.AddSubscription(connectionId, "group2");
        backplane.AddSubscription(connectionId, "group3");

        var subscriptions = backplane.GetSubscriptions(connectionId);

        subscriptions.Should().HaveCount(3);
        subscriptions.Should().Contain(new[] { "group1", "group2", "group3" });

        backplane.Dispose();
    }

    [Fact]
    public void GetSubscriptions_ReturnsEmptyForInvalidConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var subscriptions = backplane.GetSubscriptions(Guid.NewGuid());

        subscriptions.Should().BeEmpty();

        backplane.Dispose();
    }

    [Fact]
    public async Task Connection_SubscribedToMultipleGroups_ReceivesFromAll()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, "group1");
        backplane.AddSubscription(connectionId, "group2");

        await backplane.PublishToGroup("group1", new { Data = "from group1" });
        await backplane.PublishToGroup("group2", new { Data = "from group2" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(2).ToListAsync();

        received.Should().HaveCount(2);

        backplane.Dispose();
    }

    [Fact]
    public async Task PublishToGroups_DeduplicatesDeliveryToSameConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.OpenConnection();
        backplane.AddSubscription(connectionId, "group1");
        backplane.AddSubscription(connectionId, "group2");

        // Connection is subscribed to both groups, but should only receive message once
        await backplane.PublishToGroups(new[] { "group1", "group2" }, new { Data = "deduplicated" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);

        backplane.Dispose();
    }

    [Fact]
    public void Dispose_CompletesAllChannels()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, _) = backplane.OpenConnection();

        backplane.Dispose();

        var cts = new CancellationTokenSource(100);
        var act = async () => await reader.ReadAllAsync(cts.Token).ToListAsync();

        act.Should().NotThrowAsync();
    }
}
