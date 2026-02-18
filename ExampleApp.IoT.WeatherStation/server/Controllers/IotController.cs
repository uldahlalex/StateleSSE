using System.Text.Json;
using Mqtt.Controllers;
using server;

public class IotController(ILogger<IotController> logger,
    MyDbContext db
    ) : MqttController
{
    [MqttRoute("station/aaa/sensor/{sensorId}/telemetry")]
    public async Task ListenForMeasurements(Measurement m, string sensorId)
    {
        logger.LogInformation(JsonSerializer.Serialize(m));
        m.Id = Guid.NewGuid();
        db.Measurements.Add(m);
        db.SaveChanges();
    }
}