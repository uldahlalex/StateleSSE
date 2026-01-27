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
        if (_backplane1 != null) await _backplane1.DisposeAsync();
        if (_backplane2 != null) await _backplane2.DisposeAsync();
        _redis?.Dispose();

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync();
            await _redisContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task GroupAsync_SingleServer_DeliversMessage()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        await _backplane1.Groups.AddToGroupAsync(connectionId, "test-group");

        await _backplane1.Clients.GroupAsync("test-group", new TestMessage { Data = "test message" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received.Group.Should().Be("test-group");
        received.Data.GetProperty("Data").GetString().Should().Be("test message");

        await _backplane1.DisconnectAsync(connectionId);
    }

    [Fact]
    public async Task GroupAsync_MultipleServers_DeliversAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, conn1) = _backplane1.Connect();
        var (reader2, conn2) = _backplane2.Connect();
        await _backplane1.Groups.AddToGroupAsync(conn1, "chat-room");
        await _backplane2.Groups.AddToGroupAsync(conn2, "chat-room");

        await Task.Delay(100);

        await _backplane1.Clients.GroupAsync("chat-room", new TestMessage { Data = "cross-server message" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Data.GetProperty("Data").GetString().Should().Be("cross-server message");
        received2.Data.GetProperty("Data").GetString().Should().Be("cross-server message");

        await _backplane1.DisconnectAsync(conn1);
        await _backplane2.DisconnectAsync(conn2);
    }

    [Fact]
    public async Task Disconnect_StopsReceivingMessages()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        await _backplane1.Groups.AddToGroupAsync(connectionId, "test-group");

        await _backplane1.DisconnectAsync(connectionId);

        await Task.Delay(100);

        await _backplane1.Clients.GroupAsync("test-group", new TestMessage { Data = "should not receive" });

        var cts = new CancellationTokenSource(500);
        var received = await reader.ReadAllAsync(cts.Token).ToListAsync();

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveFromGroup_StopsReceivingFromGroup()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        await _backplane1.Groups.AddToGroupAsync(connectionId, "group1");
        await _backplane1.Groups.AddToGroupAsync(connectionId, "group2");

        await _backplane1.Groups.RemoveFromGroupAsync(connectionId, "group1");

        await Task.Delay(100);

        await _backplane1.Clients.GroupAsync("group1", new TestMessage { Data = "should not receive" });
        await _backplane1.Clients.GroupAsync("group2", new TestMessage { Data = "should receive" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).Take(1).ToListAsync();

        received.Should().HaveCount(1);
        received[0].Group.Should().Be("group2");
        received[0].Data.GetProperty("Data").GetString().Should().Be("should receive");

        await _backplane1.DisconnectAsync(connectionId);
    }

    [Fact]
    public async Task Connection_InMultipleGroups_ReceivesFromAll()
    {
        if (_backplane1 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader, connectionId) = _backplane1.Connect();
        await _backplane1.Groups.AddToGroupAsync(connectionId, "group1");
        await _backplane1.Groups.AddToGroupAsync(connectionId, "group2");

        await _backplane1.Clients.GroupAsync("group1", new TestMessage { Data = "from group1" });
        await _backplane1.Clients.GroupAsync("group2", new TestMessage { Data = "from group2" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await reader.ReadAllAsync(cts.Token).Take(2).ToListAsync();

        received.Should().HaveCount(2);
        received.Select(e => e.Group).Should().Contain(new[] { "group1", "group2" });

        await _backplane1.DisconnectAsync(connectionId);
    }

    [Fact]
    public async Task AllAsync_DeliversToAllConnections()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, conn1) = _backplane1.Connect();
        var (reader2, conn2) = _backplane2.Connect();

        await Task.Delay(100);

        await _backplane1.Clients.AllAsync(new TestMessage { Data = "broadcast to all" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        var received2 = await reader2.ReadAllAsync(cts.Token).FirstOrDefaultAsync();

        received1.Data.GetProperty("Data").GetString().Should().Be("broadcast to all");
        received2.Data.GetProperty("Data").GetString().Should().Be("broadcast to all");
        received1.Group.Should().BeNull();
        received2.Group.Should().BeNull();

        await _backplane1.DisconnectAsync(conn1);
        await _backplane2.DisconnectAsync(conn2);
    }

    [Fact]
    public async Task ClientAsync_DeliversToSpecificClient()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (reader1, conn1) = _backplane1.Connect();
        var (reader2, conn2) = _backplane2.Connect();

        await Task.Delay(100);

        // Send from backplane2 to a client on backplane1
        await _backplane2.Clients.ClientAsync(conn1, new TestMessage { Data = "only for conn1" });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = await reader1.ReadAllAsync(cts.Token).FirstOrDefaultAsync();
        received1.Data.GetProperty("Data").GetString().Should().Be("only for conn1");

        // conn2 should not receive anything
        reader2.TryRead(out _).Should().BeFalse();

        await _backplane1.DisconnectAsync(conn1);
        await _backplane2.DisconnectAsync(conn2);
    }

    [Fact]
    public async Task GetMemberCount_ReturnsCorrectCountAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (_, conn1) = _backplane1.Connect();
        var (_, conn2) = _backplane2.Connect();

        await _backplane1.Groups.AddToGroupAsync(conn1, "test-group");
        await _backplane2.Groups.AddToGroupAsync(conn2, "test-group");

        await Task.Delay(100);

        // Both backplanes should see 2 members
        var count1 = await _backplane1.Groups.GetMemberCountAsync("test-group");
        var count2 = await _backplane2.Groups.GetMemberCountAsync("test-group");

        count1.Should().Be(2);
        count2.Should().Be(2);

        await _backplane1.DisconnectAsync(conn1);
        await _backplane2.DisconnectAsync(conn2);
    }

    [Fact]
    public async Task GetMembers_ReturnsAllMembersAcrossServers()
    {
        if (_backplane1 == null || _backplane2 == null)
            throw new InvalidOperationException("Test not initialized");

        var (_, conn1) = _backplane1.Connect();
        var (_, conn2) = _backplane2.Connect();

        await _backplane1.Groups.AddToGroupAsync(conn1, "test-group");
        await _backplane2.Groups.AddToGroupAsync(conn2, "test-group");

        await Task.Delay(100);

        var members = await _backplane1.Groups.GetMembersAsync("test-group");

        members.Should().HaveCount(2);
        members.Should().Contain(new[] { conn1, conn2 });

        await _backplane1.DisconnectAsync(conn1);
        await _backplane2.DisconnectAsync(conn2);
    }
}

public record TestMessage
{
    public string Data { get; init; } = string.Empty;
}
