using Tapper;

namespace api.Models.BrokerToServerDtos;

[TranspilationSource]
public record WeatherDataDto(
    string StationId,
    double Temperature,
    double Humidity,
    double Pressure,
    DateTime Timestamp
);