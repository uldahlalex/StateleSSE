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
        backplane.Subscribe(connectionId, "test-channel");

        await backplane.Publish("test-channel", new { Data = "test message" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received.Channel.Should().Be("test-channel");
        received.Data.GetProperty("Data").GetString().Should().Be("test message");

        backplane.Dispose();
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_DeliversToAll()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader1, conn1) = backplane.Connect();
        var (reader2, conn2) = backplane.Connect();
        backplane.Subscribe(conn1, "test-channel");
        backplane.Subscribe(conn2, "test-channel");

        await backplane.Publish("test-channel", new { Data = "broadcast" });

        var cts = new CancellationTokenSource(100);

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Data.GetProperty("Data").GetString().Should().Be("broadcast");
        received2.Data.GetProperty("Data").GetString().Should().Be("broadcast");

        backplane.Dispose();
    }

    [Fact]
    public async Task Publish_NoSubscribers_DoesNotThrow()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var act = async () => await backplane.Publish("empty-channel", new { Data = "test" });

        await act.Should().NotThrowAsync();

        backplane.Dispose();
    }

    [Fact]
    public async Task Disconnect_NoLongerReceivesMessages()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        backplane.Subscribe(connectionId, "test-channel");

        backplane.Disconnect(connectionId);

        await backplane.Publish("test-channel", new { Data = "should not receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();

        backplane.Dispose();
    }

    [Fact]
    public async Task Unsubscribe_NoLongerReceivesFromChannel()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        backplane.Subscribe(connectionId, "channel1");
        backplane.Subscribe(connectionId, "channel2");

        backplane.Unsubscribe(connectionId, "channel1");

        await backplane.Publish("channel1", new { Data = "should not receive" });
        await backplane.Publish("channel2", new { Data = "should receive" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);
        received[0].Channel.Should().Be("channel2");
        received[0].Data.GetProperty("Data").GetString().Should().Be("should receive");

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
    public void Subscribe_ReturnsFalseForInvalidConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var result = backplane.Subscribe(Guid.NewGuid(), "test-channel");

        result.Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public void Subscribe_ReturnsFalseIfAlreadySubscribed()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (_, connectionId) = backplane.Connect();

        var first = backplane.Subscribe(connectionId, "test-channel");
        var second = backplane.Subscribe(connectionId, "test-channel");

        first.Should().BeTrue();
        second.Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public void Unsubscribe_ReturnsFalseForInvalidConnection()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);

        var result = backplane.Unsubscribe(Guid.NewGuid(), "test-channel");

        result.Should().BeFalse();

        backplane.Dispose();
    }

    [Fact]
    public async Task Connection_SubscribedToMultipleChannels_ReceivesFromAll()
    {
        var backplane = new InMemoryBackplane(NullLogger<InMemoryBackplane>.Instance);
        var (reader, connectionId) = backplane.Connect();
        backplane.Subscribe(connectionId, "channel1");
        backplane.Subscribe(connectionId, "channel2");

        await backplane.Publish("channel1", new { Data = "from channel1" });
        await backplane.Publish("channel2", new { Data = "from channel2" });

        var cts = new CancellationTokenSource(100);
        var received = await reader.ReadAllAsync(cts.Token).Take(2).ToListAsync();

        received.Should().HaveCount(2);
        received.Select(e => e.Channel).Should().Contain(new[] { "channel1", "channel2" });

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
