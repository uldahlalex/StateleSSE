using System.Text;
using System.Text.Json;

namespace StateleSSE.CodeGen;

/// <summary>
/// Generates C# EventSource clients from OpenAPI specifications.
/// </summary>
public static class CSharpEventSourceGenerator
{
    /// <summary>
    /// Generates C# EventSource client code from an OpenAPI specification file.
    /// </summary>
    /// <param name="openApiSpecPath">Path to OpenAPI JSON file (e.g., "openapi.json", "swagger.json")</param>
    /// <param name="outputPath">Output path for generated C# file</param>
    /// <param name="className">Name of the generated client class (default: "SseClient")</param>
    /// <param name="namespaceName">Namespace for the generated code (default: "GeneratedClient")</param>
    /// <param name="logOutput">Optional callback for diagnostic output. If null, writes to Console.</param>
    /// <exception cref="FileNotFoundException">Thrown when OpenAPI spec file is not found</exception>
    public static void Generate(
        string openApiSpecPath,
        string outputPath,
        string className = "SseClient",
        string namespaceName = "GeneratedClient",
        Action<string>? logOutput = null)
    {
        logOutput ??= Console.WriteLine;

        if (!File.Exists(openApiSpecPath))
            throw new FileNotFoundException($"OpenAPI spec not found: {openApiSpecPath}");

        var jsonText = File.ReadAllText(openApiSpecPath);
        var spec = JsonDocument.Parse(jsonText);
        var endpoints = FindEventSourceEndpoints(spec, logOutput);

        if (endpoints.Count == 0)
        {
            logOutput("No GET endpoints found in OpenAPI spec");
            return;
        }

        var csharp = GenerateCSharp(endpoints, className, namespaceName);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, csharp);

        logOutput($"Generated C# EventSource client at: {outputPath}");
        logOutput($"{endpoints.Count} endpoint(s) generated");
        foreach (var endpoint in endpoints)
        {
            logOutput($"- {endpoint.Path} ({endpoint.EventType})");
        }
    }

    private static List<EventSourceEndpoint> FindEventSourceEndpoints(JsonDocument spec, Action<string> logOutput)
    {
        var endpoints = new List<EventSourceEndpoint>();

        if (!spec.RootElement.TryGetProperty("paths", out var paths))
            return endpoints;

        foreach (var pathProp in paths.EnumerateObject())
        {
            var path = pathProp.Name;

            if (!pathProp.Value.TryGetProperty("get", out var operation))
                continue;

            var operationId = operation.TryGetProperty("operationId", out var opId)
                ? opId.GetString()
                : null;

            logOutput($"   Processing GET endpoint: {path}");

            if (!operation.TryGetProperty("responses", out var responses))
            {
                logOutput($"   No responses found for {path}");
                continue;
            }

            string? eventType = null;

            foreach (var responseProp in responses.EnumerateObject())
            {
                if (!responseProp.Value.TryGetProperty("content", out var content))
                    continue;

                foreach (var contentTypeProp in content.EnumerateObject())
                {
                    if (!contentTypeProp.Value.TryGetProperty("schema", out var schema))
                        continue;

                    if (!schema.TryGetProperty("$ref", out var schemaRef))
                        continue;

                    var refPath = schemaRef.GetString();
                    eventType = refPath?.Split('/').LastOrDefault();
                    break;
                }

                if (eventType != null)
                    break;
            }

            if (eventType == null)
            {
                logOutput($"   No event type found for {path}");
                continue;
            }

            logOutput($"   Found endpoint: {path} -> {eventType}");

            var summary = operation.TryGetProperty("summary", out var sum)
                ? sum.GetString()
                : null;

            var parameters = new List<EndpointParameter>();
            if (operation.TryGetProperty("parameters", out var paramsArray))
            {
                foreach (var param in paramsArray.EnumerateArray())
                {
                    if (!param.TryGetProperty("in", out var paramIn) ||
                        paramIn.GetString() != "query")
                        continue;

                    if (!param.TryGetProperty("name", out var paramName))
                        continue;

                    var name = paramName.GetString()!;
                    var isRequired = param.TryGetProperty("required", out var req) && req.GetBoolean();

                    var csType = "string";
                    if (param.TryGetProperty("schema", out var schema) &&
                        schema.TryGetProperty("type", out var schemaType))
                    {
                        csType = MapOpenApiTypeToCSharp(schemaType.GetString(), isRequired);
                    }

                    parameters.Add(new EndpointParameter(name, csType, isRequired));
                }
            }

            endpoints.Add(new EventSourceEndpoint(
                path,
                eventType!,
                operationId,
                summary,
                parameters
            ));
        }

        return endpoints;
    }

    private static string GenerateCSharp(
        List<EventSourceEndpoint> endpoints,
        string className,
        string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("public class SseMessage<T>");
        sb.AppendLine("{");
        sb.AppendLine("    public required T Data { get; init; }");
        sb.AppendLine("    public string? Event { get; init; }");
        sb.AppendLine("    public string? Id { get; init; }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public class {className}(string baseUrl, HttpClient? httpClient = null)");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly string _baseUrl = baseUrl.TrimEnd('/');");
        sb.AppendLine("    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();");
        sb.AppendLine();

        foreach (var endpoint in endpoints)
        {
            GenerateSubscriptionMethod(sb, endpoint);
        }

        sb.AppendLine("    private async IAsyncEnumerable<SseMessage<T>> StreamEventsAsync<T>(");
        sb.AppendLine("        string url,");
        sb.AppendLine("        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var request = new HttpRequestMessage(HttpMethod.Get, url);");
        sb.AppendLine("        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(\"text/event-stream\"));");
        sb.AppendLine();
        sb.AppendLine("        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);");
        sb.AppendLine("        response.EnsureSuccessStatusCode();");
        sb.AppendLine();
        sb.AppendLine("        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);");
        sb.AppendLine();
        sb.AppendLine("        var buffer = new byte[8192];");
        sb.AppendLine("        var lineBuilder = new StringBuilder();");
        sb.AppendLine("        string? data = null;");
        sb.AppendLine("        string? eventType = null;");
        sb.AppendLine("        string? id = null;");
        sb.AppendLine();
        sb.AppendLine("        while (!cancellationToken.IsCancellationRequested)");
        sb.AppendLine("        {");
        sb.AppendLine("            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);");
        sb.AppendLine("            if (bytesRead == 0) break;");
        sb.AppendLine();
        sb.AppendLine("            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);");
        sb.AppendLine("            for (var i = 0; i < text.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var c = text[i];");
        sb.AppendLine("                if (c == '\\n')");
        sb.AppendLine("                {");
        sb.AppendLine("                    var line = lineBuilder.ToString();");
        sb.AppendLine("                    lineBuilder.Clear();");
        sb.AppendLine();
        sb.AppendLine("                    if (string.IsNullOrWhiteSpace(line))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (data != null)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            var parsedData = JsonSerializer.Deserialize<T>(data);");
        sb.AppendLine("                            if (parsedData != null)");
        sb.AppendLine("                            {");
        sb.AppendLine("                                yield return new SseMessage<T>");
        sb.AppendLine("                                {");
        sb.AppendLine("                                    Data = parsedData,");
        sb.AppendLine("                                    Event = eventType,");
        sb.AppendLine("                                    Id = id");
        sb.AppendLine("                                };");
        sb.AppendLine("                            }");
        sb.AppendLine("                            data = null;");
        sb.AppendLine("                            eventType = null;");
        sb.AppendLine("                            id = null;");
        sb.AppendLine("                        }");
        sb.AppendLine("                        continue;");
        sb.AppendLine("                    }");
        sb.AppendLine();
        sb.AppendLine("                    if (line.StartsWith(\"data:\"))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        data = line[5..].TrimStart();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else if (line.StartsWith(\"event:\"))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        eventType = line[6..].TrimStart();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else if (line.StartsWith(\"id:\"))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        id = line[3..].TrimStart();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (c != '\\r')");
        sb.AppendLine("                {");
        sb.AppendLine("                    lineBuilder.Append(c);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateSubscriptionMethod(StringBuilder sb, EventSourceEndpoint endpoint)
    {
        var methodName = GenerateMethodName(endpoint);

        var hasParameters = endpoint.Parameters.Any();
        var paramList = new List<string>();

        foreach (var param in endpoint.Parameters)
        {
            var paramName = ToPascalCase(param.Name);
            var nullable = param.IsRequired ? "" : " = null";
            paramList.Add($"{param.Type} {paramName}{nullable}");
        }

        paramList.Add("CancellationToken cancellationToken = default");

        var paramString = string.Join(", ", paramList);

        sb.AppendLine($"    public IAsyncEnumerable<SseMessage<{endpoint.EventType}>> {methodName}({paramString})");
        sb.AppendLine("    {");

        if (hasParameters)
        {
            sb.AppendLine("        var queryParams = new Dictionary<string, string?>");
            sb.AppendLine("        {");
            foreach (var param in endpoint.Parameters)
            {
                var paramName = ToPascalCase(param.Name);
                if (param.IsRequired)
                {
                    sb.AppendLine($"            {{ \"{param.Name}\", {paramName}?.ToString() }},");
                }
                else
                {
                    sb.AppendLine($"            {{ \"{param.Name}\", {paramName}?.ToString() }},");
                }
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        var filteredParams = queryParams.Where(kvp => kvp.Value != null);");
            sb.AppendLine("        var queryString = string.Join(\"&\", filteredParams.Select(kvp => $\"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}\"));");
            sb.AppendLine($"        var url = $\"{{_baseUrl}}{endpoint.Path}?{{queryString}}\";");
        }
        else
        {
            sb.AppendLine($"        var url = $\"{{_baseUrl}}{endpoint.Path}\";");
        }

        sb.AppendLine();
        sb.AppendLine($"        return StreamEventsAsync<{endpoint.EventType}>(url, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string GenerateMethodName(EventSourceEndpoint endpoint)
    {
        if (string.IsNullOrEmpty(endpoint.OperationId))
            return $"Subscribe{endpoint.EventType}Async";

        if (endpoint.OperationId.Contains('_'))
        {
            var parts = endpoint.OperationId.Split('_');
            var name = parts[^1];
            return ToPascalCase(name) + "Async";
        }

        return ToPascalCase(endpoint.OperationId) + "Async";
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + input[1..];
    }

    private static string MapOpenApiTypeToCSharp(string? openApiType, bool isRequired) => openApiType switch
    {
        "string" => isRequired ? "string" : "string?",
        "integer" => isRequired ? "int" : "int?",
        "number" => isRequired ? "double" : "double?",
        "boolean" => isRequired ? "bool" : "bool?",
        "array" => "object?",
        "object" => "object?",
        _ => "object?"
    };

    private record EventSourceEndpoint(
        string Path,
        string EventType,
        string? OperationId,
        string? Summary,
        List<EndpointParameter> Parameters
    );

    private record EndpointParameter(string Name, string Type, bool IsRequired);
}
