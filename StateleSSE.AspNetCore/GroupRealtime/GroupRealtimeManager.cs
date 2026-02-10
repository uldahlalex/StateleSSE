using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace StateleSSE.AspNetCore.GroupRealtime;

internal sealed class GroupRealtimeManager : IGroupRealtimeManager
{
    private readonly ISseBackplane _backplane;
    private readonly ILogger<GroupRealtimeManager> _logger;
    private readonly ConcurrentDictionary<string, GroupRealtimeSubscription> _subscriptions = new();

    public GroupRealtimeManager(ISseBackplane backplane, ILogger<GroupRealtimeManager> logger)
    {
        _backplane = backplane;
        _logger = logger;
        _backplane.OnGroupChanged += HandleGroupChanged;
    }

    public void Subscribe(string groupName, Func<GroupChangedEventArgs, bool> criteria, Func<ISseBackplane, Task<object?>> query)
    {
        _subscriptions[groupName] = new GroupRealtimeSubscription
        {
            GroupName = groupName,
            Criteria = criteria,
            Query = query
        };
    }

    public void Unsubscribe(string groupName)
    {
        _subscriptions.TryRemove(groupName, out _);
    }

    private async void HandleGroupChanged(object? sender, GroupChangedEventArgs e)
    {
        var matched = _subscriptions.Values.Where(s => s.Criteria(e)).ToList();
        if (matched.Count == 0)
            return;

        _logger.LogDebug("GroupRealtime: {Count} subscriptions matched for {GroupName} ({Type})",
            matched.Count, e.GroupName, e.ChangeType);

        foreach (var sub in matched)
        {
            var data = await sub.Query(_backplane);
            if (data is null)
                continue;

            _logger.LogDebug("GroupRealtime: Sending to group '{Group}'", sub.GroupName);
            await _backplane.Clients.SendToGroupAsync(sub.GroupName, data);
        }
    }
}
