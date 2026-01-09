using System.Security;
using HiveMQtt.Client;
using HiveMQtt.MQTT5.Types;

namespace mqtt;

public class MqttService : IMqttService, IDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Dictionary<string, List<Func<string, string, Task>>> _subscriptionHandlers = new();
    private HiveMQClient? _client;

    public void Dispose()
    {
        _client?.DisconnectAsync().GetAwaiter().GetResult();
        _client?.Dispose();
        _connectionLock.Dispose();
    }

    public bool IsConnected => _client?.IsConnected() ?? false;

    public async Task ConnectAsync(string url, string username, string pass)
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (IsConnected)
                return;

            var parts = url.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : 1883;
            var useTls = port == 8883;

            var optionsBuilder = new HiveMQClientOptionsBuilder()
                .WithBroker(host)
                .WithPort(port);

            if (useTls)
                optionsBuilder.WithUseTls(true);

            if (!string.IsNullOrEmpty(username))
            {
                optionsBuilder.WithUserName(username);
                if (!string.IsNullOrEmpty(pass))
                {
                    var securedStringValue = new SecureString();
                    foreach (var c in pass) securedStringValue.AppendChar(c);
                    optionsBuilder.WithPassword(securedStringValue);
                }
            }

            var options = optionsBuilder.Build();
            _client = new HiveMQClient(options);

            _client.OnMessageReceived += async (sender, args) =>
            {
                var topic = args.PublishMessage.Topic;
                var payload = args.PublishMessage.PayloadAsString;

                if (_subscriptionHandlers.TryGetValue(topic, out var handlers))
                    foreach (var handler in handlers)
                        await handler(topic, payload);
            };

            await _client.ConnectAsync().ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SubscribeAsync(string topic)
    {
        if (_client == null || !IsConnected)
            throw new InvalidOperationException("MQTT client is not connected. Call ConnectAsync first.");

        if (!_subscriptionHandlers.ContainsKey(topic))
        {
            _subscriptionHandlers[topic] = new List<Func<string, string, Task>>();

            var subscribeOptions = new SubscribeOptionsBuilder()
                .WithSubscription(topic, QualityOfService.AtLeastOnceDelivery)
                .Build();

            await _client.SubscribeAsync(subscribeOptions).ConfigureAwait(false);
        }
    }

    public void RegisterHandler(string topic, Func<string, string, Task> handler)
    {
        if (!_subscriptionHandlers.ContainsKey(topic))
            _subscriptionHandlers[topic] = new List<Func<string, string, Task>>();

        _subscriptionHandlers[topic].Add(handler);
    }

    public async Task PublishAsync(string topic, string payload)
    {
        if (_client == null || !IsConnected)
            throw new InvalidOperationException("MQTT client is not connected. Call ConnectAsync first.");

        await _client.PublishAsync(topic, payload).ConfigureAwait(false);
    }
    
}