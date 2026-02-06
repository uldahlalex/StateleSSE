using Microsoft.Extensions.DependencyInjection;

namespace StateleSSE.AspNetCore.GroupRealtime;

public static class GroupRealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddGroupRealtime(this IServiceCollection services)
    {
        services.AddSingleton<GroupRealtimeManager>();
        services.AddSingleton<IGroupRealtimeManager>(sp => sp.GetRequiredService<GroupRealtimeManager>());
        return services;
    }
}
