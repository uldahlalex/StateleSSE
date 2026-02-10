using System.Text.Json;
using Mqtt.Controllers;
using server;

public class IotController : MqttController
{
    [MqttRoute("station/aaa/sensor/{sensorId}/telemetry")]
    public async Task ListenForMeasurements(Measurement m, string sensorId)
    {
        Console.WriteLine(JsonSerializer.Serialize(m));
        Console.WriteLine(sensorId);
    }
}