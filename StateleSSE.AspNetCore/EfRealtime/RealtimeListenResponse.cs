namespace StateleSSE.AspNetCore.EfRealtime;

/// <summary>
/// Returned by subscribe endpoints so the client knows which SSE group to listen on.
/// </summary>
public record RealtimeListenResponse(string Group);
