using FluentAssertions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StateleSSE.AspNetCore.Infrastructure;
using Testcontainers.Redis;

namespace StateleSSE.AspNetCore.IntegrationTests;

public class RedisBackplaneTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redis;
    private RedisBackplane? _backplane1;
    private RedisBackplane? _backplane2;

    public async Task InitializeAsync()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();

        var connectionString = _redisContainer.GetConnectionString();
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<RedisBackplane>();

        _backplane1 = new RedisBackplane(_redis, logger, "test-backplane");
        _backplane2 = new RedisBackplane(_redis, logger, "test-backplane");

        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        _backplane1?.Dispose();
        _backplane2?.Dispose();
        _redis?.Dispose();

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync();
            await _redisContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task PublishToGroup_SingleServer_DeliversMessage()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, subscriberId) = _backplane1.Subscribe("test-group");

        var message = new TestMessage { Data = "test message" };
        await _backplane1.PublishToGroup("test-group", message);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        var jsonElement = (System.Text.Json.JsonElement)received;
        var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement.GetRawText());
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Data.Should().Be(message.Data);

        _backplane1.Unsubscribe("test-group", subscriberId);
    }

    [Fact]
    public async Task PublishToGroup_MultipleServers_BroadcastsAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, sub1) = _backplane1.Subscribe("chat-room");
        var (reader2, sub2) = _backplane2.Subscribe("chat-room");

        await Task.Delay(100);

        var message = new TestMessage { Data = "cross-server message" };
        await _backplane1.PublishToGroup("chat-room", message);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        var jsonElement1 = (System.Text.Json.JsonElement)received1;
        var jsonElement2 = (System.Text.Json.JsonElement)received2;
        var receivedMessage1 = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement1.GetRawText());
        var receivedMessage2 = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement2.GetRawText());

        receivedMessage1.Should().NotBeNull();
        receivedMessage2.Should().NotBeNull();
        receivedMessage1!.Data.Should().Be(message.Data);
        receivedMessage2!.Data.Should().Be(message.Data);

        _backplane1.Unsubscribe("chat-room", sub1);
        _backplane2.Unsubscribe("chat-room", sub2);
    }

    [Fact]
    public async Task PublishToAll_DeliversToAllGroupsAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, sub1) = _backplane1.Subscribe("group1");
        var (reader2, sub2) = _backplane2.Subscribe("group2");

        await Task.Delay(100);

        var message = new TestMessage { Data = "broadcast to all" };
        await _backplane1.PublishToAll(message);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        var jsonElement1 = (System.Text.Json.JsonElement)received1;
        var jsonElement2 = (System.Text.Json.JsonElement)received2;
        var receivedMessage1 = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement1.GetRawText());
        var receivedMessage2 = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement2.GetRawText());

        receivedMessage1.Should().NotBeNull();
        receivedMessage2.Should().NotBeNull();
        receivedMessage1!.Data.Should().Be(message.Data);
        receivedMessage2!.Data.Should().Be(message.Data);

        _backplane1.Unsubscribe("group1", sub1);
        _backplane2.Unsubscribe("group2", sub2);
    }

    [Fact]
    public async Task Unsubscribe_StopsReceivingMessages()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, subscriberId) = _backplane1.Subscribe("test-group");

        _backplane1.Unsubscribe("test-group", subscriberId);

        await Task.Delay(100);

        await _backplane1.PublishToGroup("test-group", new TestMessage { Data = "should not receive" });

        var cts = new CancellationTokenSource(500);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();
    }

    [Fact]
    public void GetDiagnostics_ReturnsLocalSubscriberInfo()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (_, sub1) = _backplane1.Subscribe("group1");
        var (_, sub2) = _backplane1.Subscribe("group1");
        var (_, sub3) = _backplane1.Subscribe("group2");

        var diagnostics = _backplane1.GetDiagnostics();

        diagnostics.TotalGroups.Should().BeGreaterOrEqualTo(2);
        diagnostics.TotalLocalSubscribers.Should().BeGreaterOrEqualTo(3);

        _backplane1.Unsubscribe("group1", sub1);
        _backplane1.Unsubscribe("group1", sub2);
        _backplane1.Unsubscribe("group2", sub3);
    }
}

public record TestMessage
{
    public string Data { get; init; } = string.Empty;
}
