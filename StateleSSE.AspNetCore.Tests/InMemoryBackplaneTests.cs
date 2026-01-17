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
        var (reader, subscriberId) = backplane.Subscribe("test-group");

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
        var (reader1, sub1) = backplane.Subscribe("test-group");
        var (reader2, sub2) = backplane.Subscribe("test-group");

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
    public async Task Unsubscribe_RemovesSubscriber_NoLongerReceivesMessages()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, subscriberId) = backplane.Subscribe("test-group");

        backplane.Unsubscribe("test-group", subscriberId);

        await backplane.PublishToGroup("test-group", new { Data = "should not receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();

        backplane.Dispose();
    }

    [Fact]
    public async Task PublishToAll_DeliversToAllGroups()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, _) = backplane.Subscribe("group1");
        var (reader2, _) = backplane.Subscribe("group2");

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
        var (_, sub1) = backplane.Subscribe("test-group");
        var (_, sub2) = backplane.Subscribe("test-group");

        var count = backplane.GetLocalSubscriberCount("test-group");

        count.Should().Be(2);

        backplane.Dispose();
    }

    [Fact]
    public void GetLocalGroups_ReturnsAllGroupIds()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        backplane.Subscribe("group1");
        backplane.Subscribe("group2");
        backplane.Subscribe("group3");

        var groups = backplane.GetLocalGroups().ToList();

        groups.Should().HaveCount(3);
        groups.Should().Contain(new[] { "group1", "group2", "group3" });

        backplane.Dispose();
    }

    [Fact]
    public void GetDiagnostics_ReturnsCorrectStatistics()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        backplane.Subscribe("group1");
        backplane.Subscribe("group1");
        backplane.Subscribe("group2");

        var diagnostics = backplane.GetDiagnostics();

        diagnostics.TotalGroups.Should().Be(2);
        diagnostics.TotalLocalSubscribers.Should().Be(3);
        diagnostics.Groups.Should().HaveCount(2);

        backplane.Dispose();
    }

    [Fact]
    public void Subscribe_GeneratesUniqueSubscriberIds()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, sub1) = backplane.Subscribe("test-group");
        var (_, sub2) = backplane.Subscribe("test-group");

        sub1.Should().NotBe(sub2);
        sub1.Should().NotBe(Guid.Empty);
        sub2.Should().NotBe(Guid.Empty);

        backplane.Dispose();
    }

    [Fact]
    public void Dispose_CompletesAllChannels()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, _) = backplane.Subscribe("test-group");

        backplane.Dispose();

        var cts = new CancellationTokenSource(100);
        var act = async () => await reader.ReadAllAsync(cts.Token).ToListAsync();

        act.Should().NotThrowAsync();
    }
}
