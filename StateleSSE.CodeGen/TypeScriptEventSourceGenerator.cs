using System.Text;
using System.Text.Json;

namespace StateleSSE.CodeGen;

/// <summary>
/// Generates TypeScript EventSource clients from OpenAPI specifications.
/// </summary>
public static class TypeScriptEventSourceGenerator
{
    /// <summary>
    /// Generates TypeScript EventSource client code from an OpenAPI specification file.
    /// </summary>
    /// <param name="openApiSpecPath">Path to OpenAPI JSON file (e.g., "openapi.json", "swagger.json")</param>
    /// <param name="outputPath">Output path for generated TypeScript file</param>
    /// <param name="baseUrlImport">Import path for BASE_URL constant (default: "./utils/BASE_URL")</param>
    /// <param name="logOutput">Optional callback for diagnostic output. If null, writes to Console.</param>
    /// <exception cref="FileNotFoundException">Thrown when OpenAPI spec file is not found</exception>
    public static void Generate(
        string openApiSpecPath,
        string outputPath,
        string baseUrlImport = "./utils/BASE_URL",
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
            logOutput("No EventSource endpoints found in OpenAPI spec");
            logOutput("(Looking for GET endpoints with 'Stream' in the path name)");
            logOutput("Name your SSE endpoints with 'Stream' in the path (e.g., /StreamMessages)");
            logOutput("Or add [EventSourceEndpoint(typeof(YourEvent))] attribute for explicit marking");
            return;
        }

        var typescript = GenerateTypeScript(endpoints, baseUrlImport);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, typescript);

        logOutput($"Generated EventSource client at: {outputPath}");
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

            if (!path.Contains("Stream", StringComparison.OrdinalIgnoreCase))
                continue;

            logOutput($"   Found path with 'Stream': {path}");

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

            var operationId = operation.TryGetProperty("operationId", out var opId)
                ? opId.GetString()
                : null;

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

                    var tsType = "string";
                    if (param.TryGetProperty("schema", out var schema) &&
                        schema.TryGetProperty("type", out var schemaType))
                    {
                        tsType = MapOpenApiTypeToTypeScript(schemaType.GetString());
                    }

                    parameters.Add(new EndpointParameter(name, tsType, isRequired));
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

    private static string GenerateTypeScript(
        List<EventSourceEndpoint> endpoints,
        string baseUrlImport)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"import {{ BASE_URL }} from '{baseUrlImport}';");
        sb.AppendLine();

        sb.AppendLine("/**");
        sb.AppendLine(" * Auto-generated EventSource client");
        sb.AppendLine(" * Generated by StateleSSE.CodeGen");
        sb.AppendLine(" * DO NOT EDIT MANUALLY");
        sb.AppendLine(" */");
        sb.AppendLine();

        foreach (var endpoint in endpoints)
        {
            GenerateSubscriptionFunction(sb, endpoint);
        }

        return sb.ToString();
    }

    private static void GenerateSubscriptionFunction(StringBuilder sb, EventSourceEndpoint endpoint)
    {
        var functionName = GenerateFunctionName(endpoint);

        var hasParameters = endpoint.Parameters.Any();
        var requiredParams = endpoint.Parameters.Where(p => p.IsRequired)
            .Select(p => $"{ToCamelCase(p.Name)}: {p.Type}").ToList();
        var optionalParams = endpoint.Parameters.Where(p => !p.IsRequired)
            .Select(p => $"{ToCamelCase(p.Name)}?: {p.Type}").ToList();

        var allParams = new List<string>();
        allParams.AddRange(requiredParams);
        allParams.AddRange(optionalParams);
        allParams.Add("onMessage?: (event: T) => void");
        allParams.Add("onError?: (error: Event) => void");

        var paramList = string.Join(", ", allParams);

        sb.AppendLine("/**");
        sb.AppendLine($" * {endpoint.Summary ?? $"Subscribe to {endpoint.EventType} events"}");
        foreach (var param in endpoint.Parameters)
        {
            var optional = param.IsRequired ? "" : " (optional)";
            sb.AppendLine($" * @param {ToCamelCase(param.Name)} - {param.Name}{optional}");
        }
        sb.AppendLine(" * @param onMessage - Callback for typed message events");
        sb.AppendLine(" * @param onError - Optional error callback");
        sb.AppendLine($" * @returns EventSource instance for {endpoint.EventType}");
        sb.AppendLine(" */");

        sb.AppendLine($"export function {functionName}<T = any>({paramList}): EventSource {{");

        if (hasParameters)
        {
            var paramObj = string.Join(", ", endpoint.Parameters.Select(p =>
            {
                var paramName = ToCamelCase(p.Name);
                return $"...({paramName} !== undefined ? {{ '{p.Name}': {paramName} }} : {{}})";
            }));

            sb.AppendLine($"    const queryParams = new URLSearchParams({{ {paramObj} }});");
            sb.AppendLine($"    const url = `${{BASE_URL}}{endpoint.Path}?${{queryParams}}`;");
        }
        else
        {
            sb.AppendLine($"    const url = `${{BASE_URL}}{endpoint.Path}`;");
        }

        sb.AppendLine("    ");
        sb.AppendLine("    const es = new EventSource(url);");
        sb.AppendLine("    ");
        sb.AppendLine("    if (onMessage) {");
        sb.AppendLine("        es.onmessage = (e) => {");
        sb.AppendLine("            try {");
        sb.AppendLine("                const data: T = JSON.parse(e.data);");
        sb.AppendLine("                onMessage(data);");
        sb.AppendLine("            } catch (error) {");
        sb.AppendLine("                console.error('Failed to parse SSE event:', error);");
        sb.AppendLine("            }");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("    ");
        sb.AppendLine("    if (onError) {");
        sb.AppendLine("        es.onerror = onError;");
        sb.AppendLine("    }");
        sb.AppendLine("    ");
        sb.AppendLine("    return es;");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string GenerateFunctionName(EventSourceEndpoint endpoint)
    {
        if (string.IsNullOrEmpty(endpoint.OperationId))
            return $"subscribe{endpoint.EventType}";

        if (endpoint.OperationId.Contains('_'))
        {
            var parts = endpoint.OperationId.Split('_');
            var name = parts[^1];
            return ToCamelCase(name);
        }

        return ToCamelCase(endpoint.OperationId);
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLowerInvariant(input[0]) + input[1..];
    }

    private static string MapOpenApiTypeToTypeScript(string? openApiType) => openApiType switch
    {
        "string" => "string",
        "integer" => "number",
        "number" => "number",
        "boolean" => "boolean",
        "array" => "any[]",
        "object" => "any",
        _ => "any"
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
