namespace StateleSSE.AspNetCore;

/// <summary>
/// Marks a method as an SSE endpoint for documentation purposes.
/// Apply to controller methods alongside HTTP verb attributes.
/// Example: [HttpGet, SseEndpoint] public async Task StreamMessages() { }
/// Note: TypeScript client generation now works for all GET endpoints automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SseEndpointAttribute : Attribute
{
}
