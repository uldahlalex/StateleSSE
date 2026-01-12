#if NET9_0_OR_GREATER
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using StateleSSE.AspNetCore;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
using System.Text.Json.Nodes;
#else
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
#endif

namespace StateleSSE.AspNetCore.CodeGen;

/// <summary>
/// Microsoft.AspNetCore.OpenApi operation transformer that adds x-event-source and x-event-type extensions
/// to OpenAPI spec for endpoints marked with [EventSourceEndpoint]
/// </summary>
public sealed class MicrosoftOpenApiEventSourceTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Try to get attribute from endpoint metadata (for minimal APIs and controllers)
        var attribute = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<EventSourceEndpointAttribute>()
            .FirstOrDefault();

        // Fallback: Try to get from MethodInfo (for controller actions)
        if (attribute == null && context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerDescriptor)
        {
            attribute = controllerDescriptor.MethodInfo.GetCustomAttribute<EventSourceEndpointAttribute>();
        }

        if (attribute == null) return Task.CompletedTask;

#if NET10_0_OR_GREATER
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-event-source"] = new JsonNodeExtension(JsonValue.Create(true)!);
        operation.Extensions["x-event-type"] = new JsonNodeExtension(JsonValue.Create(attribute.EventType.Name)!);
#else
        operation.Extensions["x-event-source"] = new OpenApiBoolean(true);
        operation.Extensions["x-event-type"] = new OpenApiString(attribute.EventType.Name);
#endif

        return Task.CompletedTask;
    }
}
#endif
