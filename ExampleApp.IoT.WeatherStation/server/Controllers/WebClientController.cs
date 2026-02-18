using Microsoft.AspNetCore.Mvc;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;
using StateleSSE.AspNetCore.GroupRealtime;

namespace server.Controllers;

public class WebClientController(ISseBackplane backplane,
    IRealtimeManager realtimeManager,
    MyDbContext db,
    IGroupRealtimeManager groupRealtimeManager
) : RealtimeControllerBase(backplane)
{
    
    [HttpGet(nameof(GetMeasurements))]
    public async Task<RealtimeListenResponse<List<Measurement>>> GetMeasurements(string connectionId)
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
        return new RealtimeListenResponse<List<Measurement>>(group, db.Measurements.ToList());
    }
}