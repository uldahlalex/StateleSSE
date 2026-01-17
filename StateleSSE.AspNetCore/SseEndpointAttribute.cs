namespace StateleSSE.AspNetCore;

/// <summary>
/// Enforces "Stream" naming convention for SSE endpoints to enable CodeGen discovery.
///
/// For Minimal APIs: Apply to the path parameter
///   app.MapGet([SseEndpoint] "/StreamMessages", ...)
///
/// For Controllers: Apply to the HTTP verb attribute
///   [HttpGet, SseEndpoint]
///   public async Task StreamMessages() { }
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = false)]
public class SseEndpointAttribute : Attribute
{
}
