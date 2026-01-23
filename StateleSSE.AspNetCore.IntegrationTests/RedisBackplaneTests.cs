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

        var (reader, connectionId) = _backplane1.OpenConnection();
        _backplane1.AddSubscription(connectionId, "test-group");

        var message = new TestMessage { Data = "test message" };
        await _backplane1.PublishToGroup("test-group", message);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        var jsonElement = (System.Text.Json.JsonElement)received;
        var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement.GetRawText());
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Data.Should().Be(message.Data);

        _backplane1.CloseConnection(connectionId);
    }

    [Fact]
    public async Task PublishToGroup_MultipleServers_BroadcastsAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, conn1) = _backplane1.OpenConnection();
        var (reader2, conn2) = _backplane2.OpenConnection();
        _backplane1.AddSubscription(conn1, "chat-room");
        _backplane2.AddSubscription(conn2, "chat-room");

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

        _backplane1.CloseConnection(conn1);
        _backplane2.CloseConnection(conn2);
    }

    [Fact]
    public async Task PublishToAll_DeliversToAllConnectionsAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, conn1) = _backplane1.OpenConnection();
        var (reader2, conn2) = _backplane2.OpenConnection();
        _backplane1.AddSubscription(conn1, "group1");
        _backplane2.AddSubscription(conn2, "group2");

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

        _backplane1.CloseConnection(conn1);
        _backplane2.CloseConnection(conn2);
    }

    [Fact]
    public async Task CloseConnection_StopsReceivingMessages()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.OpenConnection();
        _backplane1.AddSubscription(connectionId, "test-group");

        _backplane1.CloseConnection(connectionId);

        await Task.Delay(100);

        await _backplane1.PublishToGroup("test-group", new TestMessage { Data = "should not receive" });

        var cts = new CancellationTokenSource(500);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSubscription_StopsReceivingFromGroup()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.OpenConnection();
        _backplane1.AddSubscription(connectionId, "group1");
        _backplane1.AddSubscription(connectionId, "group2");

        _backplane1.RemoveSubscription(connectionId, "group1");

        await Task.Delay(100);

        await _backplane1.PublishToGroup("group1", new TestMessage { Data = "should not receive" });
        await _backplane1.PublishToGroup("group2", new TestMessage { Data = "should receive" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);
        var jsonElement = (System.Text.Json.JsonElement)received[0];
        var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<TestMessage>(jsonElement.GetRawText());
        receivedMessage!.Data.Should().Be("should receive");

        _backplane1.CloseConnection(connectionId);
    }

    [Fact]
    public void GetDiagnostics_ReturnsLocalConnectionInfo()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (_, conn1) = _backplane1.OpenConnection();
        var (_, conn2) = _backplane1.OpenConnection();
        var (_, conn3) = _backplane1.OpenConnection();
        _backplane1.AddSubscription(conn1, "group1");
        _backplane1.AddSubscription(conn2, "group1");
        _backplane1.AddSubscription(conn3, "group2");

        var diagnostics = _backplane1.GetDiagnostics();

        diagnostics.TotalConnections.Should().BeGreaterOrEqualTo(3);
        diagnostics.TotalGroups.Should().BeGreaterOrEqualTo(2);
        diagnostics.TotalSubscriptions.Should().BeGreaterOrEqualTo(3);

        _backplane1.CloseConnection(conn1);
        _backplane1.CloseConnection(conn2);
        _backplane1.CloseConnection(conn3);
    }

    [Fact]
    public void GetSubscriptions_ReturnsAllGroupsForConnection()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (_, connectionId) = _backplane1.OpenConnection();
        _backplane1.AddSubscription(connectionId, "group1");
        _backplane1.AddSubscription(connectionId, "group2");
        _backplane1.AddSubscription(connectionId, "group3");

        var subscriptions = _backplane1.GetSubscriptions(connectionId);

        subscriptions.Should().HaveCount(3);
        subscriptions.Should().Contain(new[] { "group1", "group2", "group3" });

        _backplane1.CloseConnection(connectionId);
    }

    [Fact]
    public async Task Connection_SubscribedToMultipleGroups_ReceivesFromAll()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.OpenConnection();
        _backplane1.AddSubscription(connectionId, "group1");
        _backplane1.AddSubscription(connectionId, "group2");

        await _backplane1.PublishToGroup("group1", new TestMessage { Data = "from group1" });
        await _backplane1.PublishToGroup("group2", new TestMessage { Data = "from group2" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).Take(2).ToListAsync();

        received.Should().HaveCount(2);

        _backplane1.CloseConnection(connectionId);
    }
}

public record TestMessage
{
    public string Data { get; init; } = string.Empty;
}
