using System.Text;
using System.Text.Json;

namespace ChatApi.Client;

public class SseMessage<T>
{
    public required T Data { get; init; }
    public string? Event { get; init; }
    public string? Id { get; init; }
}

public class ChatSseClient(string baseUrl, HttpClient? httpClient = null)
{
    private readonly string _baseUrl = baseUrl.TrimEnd('/');
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public IAsyncEnumerable<SseMessage<Message>> StreamMessagesAsync(string? GroupId = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string?>
        {
            { "groupId", GroupId?.ToString() },
        };

        var filteredParams = queryParams.Where(kvp => kvp.Value != null);
        var queryString = string.Join("&", filteredParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}"));
        var url = $"{_baseUrl}/StreamMessages?{queryString}";

        return StreamEventsAsync<Message>(url, cancellationToken);
    }

    private async IAsyncEnumerable<SseMessage<T>> StreamEventsAsync<T>(
        string url,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[8192];
        var lineBuilder = new StringBuilder();
        string? data = null;
        string? eventType = null;
        string? id = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0) break;

            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\n')
                {
                    var line = lineBuilder.ToString();
                    lineBuilder.Clear();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (data != null)
                        {
                            var parsedData = JsonSerializer.Deserialize<T>(data);
                            if (parsedData != null)
                            {
                                yield return new SseMessage<T>
                                {
                                    Data = parsedData,
                                    Event = eventType,
                                    Id = id
                                };
                            }
                            data = null;
                            eventType = null;
                            id = null;
                        }
                        continue;
                    }

                    if (line.StartsWith("data:"))
                    {
                        data = line[5..].TrimStart();
                    }
                    else if (line.StartsWith("event:"))
                    {
                        eventType = line[6..].TrimStart();
                    }
                    else if (line.StartsWith("id:"))
                    {
                        id = line[3..].TrimStart();
                    }
                }
                else if (c != '\r')
                {
                    lineBuilder.Append(c);
                }
            }
        }
    }
}
