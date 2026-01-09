namespace api.Etc;

public static class Constants
{
    /// <summary>
    ///     Returns the SignalR group name for listening to new rounds in a game.
    /// </summary>
    /// <param name="gameId">The ID of the active game session</param>
    /// <returns>Group name in format "joinQuiz{gameId}"</returns>
    public static string JoinQuizGroup(string gameId)
    {
        return "joinQuiz" + gameId;
    }

    /// <summary>
    ///     Returns the SignalR group name for listening to round results in a game.
    /// </summary>
    /// <param name="gameId">The ID of the active game session</param>
    /// <returns>Group name in format "ListenForResults{gameId}"</returns>
    public static string ListenForResultsGroup(string gameId)
    {
        return "ListenForResults" + gameId;
    }

    /// <summary>
    ///     SignalR hub endpoints
    /// </summary>
    public static class HubEndpoints
    {
        public const string Quiz = "/quizhub";
        public const string Realtime = "/realtimehub";
    }

    /// <summary>
    ///     MQTT topic names
    /// </summary>
    public static class MqttTopics
    {
        public const string WeatherData = "weather/data";
        public const string WeatherPreferences = "weather/preferences";
    }

    /// <summary>
    ///     HTTP and SignalR authentication constants
    /// </summary>
    public static class Auth
    {
        public const string BearerPrefix = "Bearer ";
        public const string AccessTokenQueryParameter = "access_token";
    }
}