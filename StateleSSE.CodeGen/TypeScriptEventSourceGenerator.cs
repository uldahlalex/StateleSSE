using System.Text;
using System.Text.Json;

namespace StateleSSE.CodeGen;

/// <summary>
/// Zero-dependency TypeScript EventSource client generator from OpenAPI specifications.
/// Reads openapi.json files and generates type-safe EventSource clients for SSE endpoints.
/// </summary>
public static class TypeScriptEventSourceGenerator
{
    /// <summary>
    /// Generates TypeScript EventSource client code from an OpenAPI specification file.
    /// </summary>
    /// <param name="openApiSpecPath">Path to OpenAPI JSON file (e.g., "openapi.json", "swagger.json")</param>
    /// <param name="outputPath">Output path for generated TypeScript file</param>
    /// <param name="baseUrlImport">Import path for BASE_URL constant (default: "./utils/BASE_URL")</param>
    /// <exception cref="FileNotFoundException">Thrown when OpenAPI spec file is not found</exception>
    public static void Generate(
        string openApiSpecPath,
        string outputPath,
        string baseUrlImport = "./utils/BASE_URL")
    {
        if (!File.Exists(openApiSpecPath))
            throw new FileNotFoundException($"OpenAPI spec not found: {openApiSpecPath}");

        var jsonText = File.ReadAllText(openApiSpecPath);
        var spec = JsonDocument.Parse(jsonText);
        var endpoints = FindEventSourceEndpoints(spec);

        if (endpoints.Count == 0)
        {
            Console.WriteLine("⚠️  No EventSource endpoints found in OpenAPI spec");
            Console.WriteLine("   (Looking for GET endpoints with 'Stream' in the path name)");
            Console.WriteLine("   Name your SSE endpoints with 'Stream' in the path (e.g., /StreamMessages)");
            Console.WriteLine("   Or add [EventSourceEndpoint(typeof(YourEvent))] attribute for explicit marking");
            return;
        }

        var typescript = GenerateTypeScript(endpoints, baseUrlImport);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, typescript);

        Console.WriteLine($"✅ Generated EventSource client at: {outputPath}");
        Console.WriteLine($"   {endpoints.Count} endpoint(s) generated");
        foreach (var endpoint in endpoints)
        {
            Console.WriteLine($"   - {endpoint.Path} ({endpoint.EventType})");
        }
    }

    private static List<EventSourceEndpoint> FindEventSourceEndpoints(JsonDocument spec)
    {
        var endpoints = new List<EventSourceEndpoint>();

        if (!spec.RootElement.TryGetProperty("paths", out var paths))
            return endpoints;

        foreach (var pathProp in paths.EnumerateObject())
        {
            var path = pathProp.Name;

            // Only look at GET operations (EventSource only supports GET)
            if (!pathProp.Value.TryGetProperty("get", out var operation))
                continue;

            // Convention-based detection: endpoint name must contain "Stream"
            if (!path.Contains("Stream", StringComparison.OrdinalIgnoreCase))
                continue;

            Console.WriteLine($"   Found path with 'Stream': {path}");

            if (!operation.TryGetProperty("responses", out var responses))
            {
                Console.WriteLine($"   ✗ No responses found for {path}");
                continue;
            }

            string? eventType = null;

            // Extract event type from any response schema (framework-agnostic)
            foreach (var responseProp in responses.EnumerateObject())
            {
                if (!responseProp.Value.TryGetProperty("content", out var content))
                    continue;

                // Look for schema in any content type (text/event-stream, application/json, etc.)
                foreach (var contentTypeProp in content.EnumerateObject())
                {
                    if (contentTypeProp.Value.TryGetProperty("schema", out var schema))
                    {
                        if (schema.TryGetProperty("$ref", out var schemaRef))
                        {
                            // Extract type name from $ref like "#/components/schemas/Message"
                            var refPath = schemaRef.GetString();
                            eventType = refPath?.Split('/').LastOrDefault();
                            break;
                        }
                    }
                }

                if (eventType != null)
                    break;
            }

            // Skip if no event type found
            if (eventType == null)
            {
                Console.WriteLine($"   ✗ No event type found for {path}");
                continue;
            }

            Console.WriteLine($"   ✓ Found endpoint: {path} -> {eventType}");

            // Extract operation ID for function naming
            var operationId = operation.TryGetProperty("operationId", out var opId)
                ? opId.GetString()
                : null;

            // Extract summary for JSDoc
            var summary = operation.TryGetProperty("summary", out var sum)
                ? sum.GetString()
                : null;

            // Extract query parameters
            var parameters = new List<EndpointParameter>();
            if (operation.TryGetProperty("parameters", out var paramsArray))
            {
                foreach (var param in paramsArray.EnumerateArray())
                {
                    // Only include query parameters
                    if (!param.TryGetProperty("in", out var paramIn) ||
                        paramIn.GetString() != "query")
                        continue;

                    if (!param.TryGetProperty("name", out var paramName))
                        continue;

                    var name = paramName.GetString()!;
                    var isRequired = param.TryGetProperty("required", out var req) && req.GetBoolean();

                    // Determine TypeScript type from schema
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

        // Import BASE_URL
        sb.AppendLine($"import {{ BASE_URL }} from '{baseUrlImport}';");
        sb.AppendLine();

        // File header
        sb.AppendLine("/**");
        sb.AppendLine(" * Auto-generated EventSource client");
        sb.AppendLine(" * Generated by StateleSSE.CodeGen");
        sb.AppendLine(" * DO NOT EDIT MANUALLY");
        sb.AppendLine(" */");
        sb.AppendLine();

        // Generate subscription function for each endpoint
        foreach (var endpoint in endpoints)
        {
            GenerateSubscriptionFunction(sb, endpoint);
        }

        return sb.ToString();
    }

    private static void GenerateSubscriptionFunction(StringBuilder sb, EventSourceEndpoint endpoint)
    {
        var functionName = GenerateFunctionName(endpoint);

        // Build parameter list: required params first, then optional params
        var hasParameters = endpoint.Parameters.Any();
        var requiredParams = endpoint.Parameters.Where(p => p.IsRequired)
            .Select(p => $"{p.Name.ToLowerInvariant()}: {p.Type}").ToList();
        var optionalParams = endpoint.Parameters.Where(p => !p.IsRequired)
            .Select(p => $"{p.Name.ToLowerInvariant()}?: {p.Type}").ToList();

        var allParams = new List<string>();
        allParams.AddRange(requiredParams);
        allParams.AddRange(optionalParams);
        allParams.Add("onMessage?: (event: T) => void");
        allParams.Add("onError?: (error: Event) => void");

        var paramList = string.Join(", ", allParams);

        // JSDoc comment
        sb.AppendLine("/**");
        sb.AppendLine($" * {endpoint.Summary ?? $"Subscribe to {endpoint.EventType} events"}");
        foreach (var param in endpoint.Parameters)
        {
            var optional = param.IsRequired ? "" : " (optional)";
            sb.AppendLine($" * @param {param.Name.ToLowerInvariant()} - {param.Name}{optional}");
        }
        sb.AppendLine(" * @param onMessage - Callback for typed message events");
        sb.AppendLine(" * @param onError - Optional error callback");
        sb.AppendLine($" * @returns EventSource instance for {endpoint.EventType}");
        sb.AppendLine(" */");

        // Function declaration with generic type
        sb.AppendLine($"export function {functionName}<T = any>({paramList}): EventSource {{");

        // Build query string from parameters
        if (hasParameters)
        {
            var paramObj = string.Join(", ", endpoint.Parameters.Select(p =>
            {
                var name = p.Name.ToLowerInvariant();
                return p.IsRequired
                    ? $"...({name} !== undefined ? {{ {name} }} : {{}})"
                    : $"...({name} !== undefined ? {{ {name} }} : {{}})";
            }));

            sb.AppendLine($"    const queryParams = new URLSearchParams({{ {paramObj} }});");
            sb.AppendLine($"    const url = `${{BASE_URL}}{endpoint.Path}?${{queryParams}}`;");
        }
        else
        {
            sb.AppendLine($"    const url = `${{BASE_URL}}{endpoint.Path}`;");
        }

        // Create EventSource with handlers
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
        // Try to use operationId if available
        if (!string.IsNullOrEmpty(endpoint.OperationId))
        {
            // If operationId contains underscore (e.g., "Messages_StreamMessages")
            // use the last part
            if (endpoint.OperationId.Contains('_'))
            {
                var parts = endpoint.OperationId.Split('_');
                var name = parts[^1];
                return ToCamelCase(name);
            }

            return ToCamelCase(endpoint.OperationId);
        }

        // Fallback: generate from event type
        return $"subscribe{endpoint.EventType}";
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
