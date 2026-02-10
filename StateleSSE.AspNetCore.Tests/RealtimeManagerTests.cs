using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;
using StateleSSE.AspNetCore.Infrastructure;

namespace StateleSSE.AspNetCore.Tests;

public class RealtimeManagerTests
{
    private static RealtimeManager CreateManager(out InMemoryBackplane backplane)
    {
        backplane = new InMemoryBackplane();
        return new RealtimeManager(backplane);
    }

    private static void SubscribeFake(RealtimeManager mgr, string connectionId, string groupName)
    {
        mgr.Subscribe<DbContext>(connectionId, groupName,
            _ => true,
            _ => Task.FromResult<object?>(null));
    }

    [Fact]
    public void Subscribe_SameGroup_TwoConnections_BothTracked()
    {
        var mgr = CreateManager(out _);
        SubscribeFake(mgr, "conn-1", "room:abc");
        SubscribeFake(mgr, "conn-2", "room:abc");

        var subs = mgr.GetSubscriptionsForContext(typeof(DbContext));
        subs.Should().HaveCount(1);
        subs[0].ConnectionIds.Should().Contain(["conn-1", "conn-2"]);
    }

    [Fact]
    public void Unsubscribe_OneOfTwo_SubscriptionSurvives()
    {
        var mgr = CreateManager(out _);
        SubscribeFake(mgr, "conn-1", "room:abc");
        SubscribeFake(mgr, "conn-2", "room:abc");

        mgr.Unsubscribe("conn-1", "room:abc");

        var subs = mgr.GetSubscriptionsForContext(typeof(DbContext));
        subs.Should().HaveCount(1);
        subs[0].ConnectionIds.Should().Contain("conn-2");
        subs[0].ConnectionIds.Should().NotContain("conn-1");
    }

    [Fact]
    public void Unsubscribe_LastConnection_RemovesSubscription()
    {
        var mgr = CreateManager(out _);
        SubscribeFake(mgr, "conn-1", "room:abc");

        mgr.Unsubscribe("conn-1", "room:abc");

        mgr.GetSubscriptionsForContext(typeof(DbContext)).Should().BeEmpty();
    }

    [Fact]
    public void UnsubscribeAll_RemovesConnectionFromAllGroups()
    {
        var mgr = CreateManager(out _);
        SubscribeFake(mgr, "conn-1", "group-a");
        SubscribeFake(mgr, "conn-1", "group-b");
        SubscribeFake(mgr, "conn-2", "group-a");

        mgr.UnsubscribeAll("conn-1");

        var subs = mgr.GetSubscriptionsForContext(typeof(DbContext));
        subs.Should().HaveCount(1);
        subs[0].GroupName.Should().Be("group-a");
        subs[0].ConnectionIds.Should().Equal(["conn-2"]);
    }

    [Fact]
    public async Task OnClientDisconnected_CleansUpSubscriptions()
    {
        var mgr = CreateManager(out var backplane);
        var (_, connId) = backplane.Connect();

        SubscribeFake(mgr, connId, "room:abc");
        mgr.GetSubscriptionsForContext(typeof(DbContext)).Should().HaveCount(1);

        await backplane.DisconnectAsync(connId);

        mgr.GetSubscriptionsForContext(typeof(DbContext)).Should().BeEmpty();
    }

    [Fact]
    public async Task OnClientDisconnected_OtherConnectionsSurvive()
    {
        var mgr = CreateManager(out var backplane);
        var (_, conn1) = backplane.Connect();
        var (_, conn2) = backplane.Connect();

        SubscribeFake(mgr, conn1, "room:abc");
        SubscribeFake(mgr, conn2, "room:abc");

        await backplane.DisconnectAsync(conn1);

        var subs = mgr.GetSubscriptionsForContext(typeof(DbContext));
        subs.Should().HaveCount(1);
        subs[0].ConnectionIds.Should().Contain(conn2);
        subs[0].ConnectionIds.Should().NotContain(conn1);
    }

    [Fact]
    public void Subscribe_SameGroupMultipleConnections_ReturnsOneSubscription()
    {
        var mgr = CreateManager(out _);
        SubscribeFake(mgr, "conn-1", "room:abc");
        SubscribeFake(mgr, "conn-2", "room:abc");
        SubscribeFake(mgr, "conn-3", "room:abc");

        var subs = mgr.GetSubscriptionsForContext(typeof(DbContext));
        subs.Should().HaveCount(1);
        subs[0].ConnectionIds.Should().HaveCount(3);
    }
}
