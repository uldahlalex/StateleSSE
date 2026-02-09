using Microsoft.Extensions.DependencyInjection;

namespace StateleSSE.AspNetCore.GroupRealtime;

public static class GroupRealtimeServiceCollectionExtensions
{
    /// <summary>
    /// Dependency injection for IGroupRealtimeManager + GroupRealtimeManager
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddGroupRealtime(this IServiceCollection services)
    {
        services.AddSingleton<GroupRealtimeManager>();
        services.AddSingleton<IGroupRealtimeManager>(sp => sp.GetRequiredService<GroupRealtimeManager>());
        return services;
    }
}
