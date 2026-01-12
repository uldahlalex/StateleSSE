#if !NET10_0_OR_GREATER
using System.Reflection;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using StateleSSE.AspNetCore;

namespace StateleSSE.AspNetCore.CodeGen;

/// <summary>
/// NSwag operation processor that adds x-event-source and x-event-type extensions
/// to OpenAPI spec for endpoints marked with [EventSourceEndpoint]
/// </summary>
public sealed class NSwagEventSourceProcessor : IOperationProcessor
{
    /// <inheritdoc />
    public bool Process(OperationProcessorContext context)
    {
        var attribute = context.MethodInfo.GetCustomAttribute<EventSourceEndpointAttribute>();
        if (attribute == null) return true;

        context.OperationDescription.Operation.ExtensionData ??= new Dictionary<string, object?>();
        context.OperationDescription.Operation.ExtensionData["x-event-source"] = true;
        context.OperationDescription.Operation.ExtensionData["x-event-type"] = attribute.EventType.Name;

        return true;
    }
}
#endif
