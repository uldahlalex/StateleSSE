namespace mqtt;

public interface IMqttService
{
    bool IsConnected { get; }
    Task ConnectAsync(string url, string username, string pass);
    Task PublishAsync(string topic, string payload);
    
    /// <summary>
    /// Should this lookup subscriptions in persistence and maybe also persist data upon retrival (which in return will broadcast to web clients)
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="messageHandler"></param>
    /// <returns></returns>
    Task SubscribeAsync(string topic);

    void RegisterHandler(string topic, Func<string, string, Task> handler);
}