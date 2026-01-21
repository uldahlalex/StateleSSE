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
    /// <param name="modelsImport">Optional import path for model types (e.g., "./generated-client.ts"). If null, uses generic types.</param>
    /// <param name="logOutput">Optional callback for diagnostic output. If null, writes to Console.</param>
    /// <exception cref="FileNotFoundException">Thrown when OpenAPI spec file is not found</exception>
    public static void Generate(
        string openApiSpecPath,
        string outputPath,
        string baseUrlImport = "./utils/BASE_URL",
        string? modelsImport = null,
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

        var typescript = GenerateTypeScript(endpoints, baseUrlImport, modelsImport);

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
            List<string>? multipleEventTypes = null;

            foreach (var responseProp in responses.EnumerateObject())
            {
                if (!responseProp.Value.TryGetProperty("content", out var content))
                    continue;

                foreach (var contentTypeProp in content.EnumerateObject())
                {
                    if (!contentTypeProp.Value.TryGetProperty("schema", out var schema))
                        continue;

                    if (schema.TryGetProperty("$ref", out var schemaRef))
                    {
                        var refPath = schemaRef.GetString();
                        var typeName = refPath?.Split('/').LastOrDefault();

                        if (typeName != null && typeName.EndsWith("Union"))
                        {
                            multipleEventTypes = ExtractUnionTypes(spec, typeName);
                            eventType = typeName;
                        }
                        else
                        {
                            eventType = typeName;
                        }
                        break;
                    }
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
                parameters,
                multipleEventTypes
            ));
        }

        return endpoints;
    }

    private static List<string> ExtractUnionTypes(JsonDocument spec, string unionTypeName)
    {
        var types = new List<string>();

        if (!spec.RootElement.TryGetProperty("components", out var components))
            return types;

        if (!components.TryGetProperty("schemas", out var schemas))
            return types;

        if (!schemas.TryGetProperty(unionTypeName, out var unionSchema))
            return types;

        if (!unionSchema.TryGetProperty("properties", out var properties))
            return types;

        foreach (var prop in properties.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("$ref", out var refValue))
            {
                var typeName = refValue.GetString()?.Split('/').LastOrDefault();
                if (typeName != null && !typeName.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    types.Add(typeName);
                }
            }
        }

        return types;
    }

    private static string GenerateTypeScript(
        List<EventSourceEndpoint> endpoints,
        string baseUrlImport,
        string? modelsImport)
    {
        var sb = new StringBuilder();
        var hasModelsImport = !string.IsNullOrEmpty(modelsImport);

        sb.AppendLine("/* eslint-disable */");
        sb.AppendLine("// @ts-nocheck");
        sb.AppendLine();
        sb.AppendLine($"import {{ BASE_URL }} from '{baseUrlImport}';");

        if (hasModelsImport)
        {
            var allEventTypes = endpoints
                .SelectMany(e => e.MultipleEventTypes ?? new List<string> { e.EventType })
                .Distinct()
                .OrderBy(t => t);
            var typeImports = string.Join(", ", allEventTypes);
            sb.AppendLine($"import type {{ {typeImports} }} from '{modelsImport}';");
        }

        sb.AppendLine();

        sb.AppendLine("/**");
        sb.AppendLine(" * Auto-generated EventSource client");
        sb.AppendLine(" * Generated by StateleSSE.CodeGen");
        sb.AppendLine(" * DO NOT EDIT MANUALLY");
        sb.AppendLine(" */");
        sb.AppendLine();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.MultipleEventTypes != null && endpoint.MultipleEventTypes.Count > 0)
            {
                GenerateMultiEventSubscriptionFunction(sb, endpoint, modelsImport);
            }
            else
            {
                GenerateSubscriptionFunction(sb, endpoint, modelsImport);

                if (hasModelsImport)
                {
                    GenerateGenericSubscriptionFunction(sb, endpoint);
                }
            }
        }

        return sb.ToString();
    }

    private static void GenerateSubscriptionFunction(StringBuilder sb, EventSourceEndpoint endpoint, string? modelsImport)
    {
        var functionName = GenerateFunctionName(endpoint);
        var hasModelsImport = !string.IsNullOrEmpty(modelsImport);
        var eventType = hasModelsImport ? endpoint.EventType : "T";

        var hasParameters = endpoint.Parameters.Any();
        var requiredParams = endpoint.Parameters.Where(p => p.IsRequired)
            .Select(p => $"{ToCamelCase(p.Name)}: {p.Type}").ToList();
        var optionalParams = endpoint.Parameters.Where(p => !p.IsRequired)
            .Select(p => $"{ToCamelCase(p.Name)}?: {p.Type}").ToList();

        var allParams = new List<string>();
        allParams.AddRange(requiredParams);
        allParams.AddRange(optionalParams);
        allParams.Add($"onMessage?: (event: {eventType}) => void");
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
        sb.AppendLine(" * @param onError - Optional error callback. Triggered on connection errors, network failures, or when server closes the connection");
        sb.AppendLine($" * @returns EventSource instance for {endpoint.EventType}");
        sb.AppendLine(" */");

        var functionSignature = hasModelsImport
            ? $"export function {functionName}({paramList}): EventSource {{"
            : $"export function {functionName}<T = any>({paramList}): EventSource {{";

        sb.AppendLine(functionSignature);

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
        sb.AppendLine($"                const data: {eventType} = JSON.parse(e.data);");
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

    private static void GenerateGenericSubscriptionFunction(StringBuilder sb, EventSourceEndpoint endpoint)
    {
        var functionName = GenerateFunctionName(endpoint);
        var hasParameters = endpoint.Parameters.Any();

        var allParams = new List<string>();
        allParams.Add("params?: any");
        allParams.Add("onMessage?: (event: T) => void");
        allParams.Add("onError?: (error: Event) => void");

        var paramList = string.Join(", ", allParams);

        sb.AppendLine("/**");
        sb.AppendLine($" * Generic version of {functionName} that accepts any parameters and returns any type");
        sb.AppendLine(" * Use this for custom scenarios where you need full control over types");
        sb.AppendLine(" * @param params - Query parameters as any object");
        sb.AppendLine(" * @param onMessage - Callback for typed message events");
        sb.AppendLine(" * @param onError - Optional error callback. Triggered on connection errors, network failures, or when server closes the connection");
        sb.AppendLine($" * @returns EventSource instance");
        sb.AppendLine(" */");
        sb.AppendLine($"export function {functionName}Generic<T = any>({paramList}): EventSource {{");

        sb.AppendLine($"    const queryParams = params ? new URLSearchParams(params) : new URLSearchParams();");
        sb.AppendLine($"    const url = `${{BASE_URL}}{endpoint.Path}?${{queryParams}}`;");

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

    private static void GenerateMultiEventSubscriptionFunction(StringBuilder sb, EventSourceEndpoint endpoint, string? modelsImport)
    {
        var functionName = GenerateFunctionName(endpoint);
        var hasModelsImport = !string.IsNullOrEmpty(modelsImport);

        var requiredParams = endpoint.Parameters.Where(p => p.IsRequired)
            .Select(p => $"{ToCamelCase(p.Name)}: {p.Type}").ToList();
        var optionalParams = endpoint.Parameters.Where(p => !p.IsRequired)
            .Select(p => $"{ToCamelCase(p.Name)}?: {p.Type}").ToList();

        var allParams = new List<string>();
        allParams.AddRange(requiredParams);
        allParams.AddRange(optionalParams);

        var paramList = allParams.Count > 0 ? string.Join(", ", allParams) : "";

        sb.AppendLine("/**");
        sb.AppendLine($" * {endpoint.Summary ?? $"Subscribe to multiple event types on a single connection"}");
        sb.AppendLine($" * Streams: {string.Join(", ", endpoint.MultipleEventTypes!)}");
        foreach (var param in endpoint.Parameters)
        {
            var optional = param.IsRequired ? "" : " (optional)";
            sb.AppendLine($" * @param {ToCamelCase(param.Name)} - {param.Name}{optional}");
        }
        sb.AppendLine($" * @returns EventSource instance with typed event listeners");
        sb.AppendLine(" */");
        sb.AppendLine($"export function {functionName}({paramList}) {{");

        if (endpoint.Parameters.Any())
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

        sb.AppendLine("    const es = new EventSource(url);");
        sb.AppendLine("    ");
        sb.AppendLine("    return {");
        sb.AppendLine("        eventSource: es,");

        foreach (var eventType in endpoint.MultipleEventTypes!)
        {
            var handlerName = $"on{eventType.Replace("Event", "")}";
            sb.AppendLine($"        {handlerName}: (callback: (data: {eventType}) => void) => {{");
            sb.AppendLine($"            es.addEventListener('{eventType}', (e) => {{");
            sb.AppendLine("                try {");
            sb.AppendLine($"                    const data: {eventType} = JSON.parse((e as MessageEvent).data);");
            sb.AppendLine("                    callback(data);");
            sb.AppendLine("                } catch (error) {");
            sb.AppendLine($"                    console.error('Failed to parse {eventType}:', error);");
            sb.AppendLine("                }");
            sb.AppendLine("            });");
            sb.AppendLine("            return this;");
            sb.AppendLine("        },");
        }

        sb.AppendLine("        onError: (callback: (error: Event) => void) => {");
        sb.AppendLine("            es.onerror = callback;");
        sb.AppendLine("            return this;");
        sb.AppendLine("        },");
        sb.AppendLine("        close: () => es.close()");
        sb.AppendLine("    };");
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
        List<EndpointParameter> Parameters,
        List<string>? MultipleEventTypes = null
    );

    private record EndpointParameter(string Name, string Type, bool IsRequired);
}
