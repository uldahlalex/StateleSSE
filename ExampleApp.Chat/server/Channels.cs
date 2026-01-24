namespace ExampleApp.Chat;

/// <summary>
/// Single source of truth for channel patterns.
/// Convention: channel name mirrors the REST endpoint path.
/// Const fields are exposed via OpenAPI → NSwag generates TypeScript constants.
/// </summary>
public static class Channels
{
    // Patterns (exposed to OpenAPI via StringConstantsDocumentProcessor)
    public const string RoomMessages = "rooms/{roomId}/messages";
    public const string RoomTyping = "rooms/{roomId}/typing";
    public const string RoomPresence = "rooms/{roomId}/presence";

    // Resolve pattern with actual values (server-side usage)
    public static string Resolve(string pattern, string roomId) =>
        pattern.Replace("{roomId}", roomId);
}
