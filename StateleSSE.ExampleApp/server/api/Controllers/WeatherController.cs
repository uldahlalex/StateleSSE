// using api.Models.SseEventDtos;
// using Microsoft.AspNetCore.Mvc;
// using StateleSSE.Backplane.Redis;
// using StateleSSE.Backplane.Redis.Attributes;
//
// namespace api.Controllers;
//
// public record WeatherStationSubscribeRequestDto(string StationId);
//
// [ApiController]
// public class WeatherController(StateleSSE.Backplane.Redis.Infrastructure.RedisBackplane backplane)
//     : SseControllerBase(backplane)
// {
//     /// <summary>
//     /// Stream WeatherDataEvent for a specific weather station
//     /// </summary>
//     [HttpGet("weather/station")]
//     [EventSourceEndpoint(typeof(WeatherDataEvent))]
//     public async Task StreamWeatherStation([FromQuery] WeatherStationSubscribeRequestDto dto)
//     {
//         var channel = $"weather:{dto.StationId}";
//         await StreamEventType<WeatherDataEvent>(channel);
//     }
//
//     /// <summary>
//     /// Stream WeatherDataEvent from all weather stations
//     /// </summary>
//     [HttpGet("weather/all")]
//     [EventSourceEndpoint(typeof(WeatherDataEvent))]
//     public async Task StreamAllWeatherStations()
//     {
//         var channel = "weather:all";
//         await StreamEventType<WeatherDataEvent>(channel);
//     }
// }
