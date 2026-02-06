using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace StateleSSE.AspNetCore.EfRealtime;

/// <summary>
/// Returned by subscribe endpoints so the client knows which SSE group to listen on.
/// </summary>
public record RealtimeListenResponse([Required][NotNull]string Group = null!);

/// <summary>
/// Returned by subscribe endpoints with initial data. The client receives the current state
/// immediately and knows which SSE group to listen on for subsequent updates.
/// </summary>
/// <typeparam name="T">The type of the initial data payload.</typeparam>
public record RealtimeListenResponse<T>(
    [Required][NotNull] string Group = null!,
    T? Data = default
) : RealtimeListenResponse(Group);
