using server.Services;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;
using StateleSSE.AspNetCore.GroupRealtime;

namespace server.Controllers;

public class WebClientController(ISseBackplane backplane,
    IRealtimeManager realtimeManager,
    IGroupRealtimeManager groupRealtimeManager,
    WeatherService weatherService
) : RealtimeControllerBase(backplane)
{
    public async Task GetMeasurements(string connectionId)
    {
        var group = "measurements";
        await backplane.Groups.AddToGroupAsync(connectionId, group);
        realtimeManager.Subscribe<MyDbContext>(connectionId, group, 
            criteria: snapshot =>
            {
                return snapshot.HasChanges<Measurement>();
            },
            query: async context =>
            {
                return context.Measurements.ToList();
            }
            );
    }
}