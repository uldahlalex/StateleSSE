using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace StateleSSE.AspNetCore;

/// <summary>
/// This exists because it needs to send the connection ID to the client upon opening the connection.
/// It is merely a wrapper for a string.
/// </summary>
/// <param name="ConnectionId"></param>
public record RealtimeConnectionResponse(string ConnectionId);

public abstract class RealtimeControllerBase(ISseBackplane backplane) : ControllerBase
{
    protected ISseBackplane Backplane => backplane;

    protected virtual string? GetConnectionUserId() => null;

    [HttpGet("sse")]
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync();
        await using var connection = backplane.CreateConnection(GetConnectionUserId());

        await sse.WriteAsync("connected", JsonSerializer.Serialize(
            new RealtimeConnectionResponse(connection.ConnectionId),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        await foreach (var evt in connection.ReadAllAsync(HttpContext.RequestAborted))
        {
            if (evt.Group != null)
                await sse.WriteAsync(evt.Group, evt.Data);
            else
                await sse.WriteAsync(evt.Data);
        }
    }
}
