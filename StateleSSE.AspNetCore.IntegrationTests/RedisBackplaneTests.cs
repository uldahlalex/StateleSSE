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

        _backplane1 = new RedisBackplane(_redis, logger, "test");
        _backplane2 = new RedisBackplane(_redis, logger, "test");

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
    public async Task Publish_SingleServer_DeliversMessage()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        _backplane1.Subscribe(connectionId, "test-channel");

        await _backplane1.Publish("test-channel", new TestMessage { Data = "test message" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received.Channel.Should().Be("test-channel");
        received.Data.GetProperty("Data").GetString().Should().Be("test message");

        _backplane1.Disconnect(connectionId);
    }

    [Fact]
    public async Task Publish_MultipleServers_BroadcastsAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, conn1) = _backplane1.Connect();
        var (reader2, conn2) = _backplane2.Connect();
        _backplane1.Subscribe(conn1, "chat-room");
        _backplane2.Subscribe(conn2, "chat-room");

        await Task.Delay(100);

        await _backplane1.Publish("chat-room", new TestMessage { Data = "cross-server message" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Data.GetProperty("Data").GetString().Should().Be("cross-server message");
        received2.Data.GetProperty("Data").GetString().Should().Be("cross-server message");

        _backplane1.Disconnect(conn1);
        _backplane2.Disconnect(conn2);
    }

    [Fact]
    public async Task Disconnect_StopsReceivingMessages()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        _backplane1.Subscribe(connectionId, "test-channel");

        _backplane1.Disconnect(connectionId);

        await Task.Delay(100);

        await _backplane1.Publish("test-channel", new TestMessage { Data = "should not receive" });

        var cts = new CancellationTokenSource(500);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribe_StopsReceivingFromChannel()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        _backplane1.Subscribe(connectionId, "channel1");
        _backplane1.Subscribe(connectionId, "channel2");

        _backplane1.Unsubscribe(connectionId, "channel1");

        await Task.Delay(100);

        await _backplane1.Publish("channel1", new TestMessage { Data = "should not receive" });
        await _backplane1.Publish("channel2", new TestMessage { Data = "should receive" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);
        received[0].Channel.Should().Be("channel2");
        received[0].Data.GetProperty("Data").GetString().Should().Be("should receive");

        _backplane1.Disconnect(connectionId);
    }

    [Fact]
    public async Task Connection_SubscribedToMultipleChannels_ReceivesFromAll()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        _backplane1.Subscribe(connectionId, "channel1");
        _backplane1.Subscribe(connectionId, "channel2");

        await _backplane1.Publish("channel1", new TestMessage { Data = "from channel1" });
        await _backplane1.Publish("channel2", new TestMessage { Data = "from channel2" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).Take(2).ToListAsync();

        received.Should().HaveCount(2);

        _backplane1.Disconnect(connectionId);
    }
}

public record TestMessage
{
    public string Data { get; init; } = string.Empty;
}
