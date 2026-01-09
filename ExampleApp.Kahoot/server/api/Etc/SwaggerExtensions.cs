using System.Reflection;
using StateleSSE.AspNetCore;
using NJsonSchema;
using NSwag.Generation;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace api.Extensions;

public static class SwaggerExtensions
{
    public static void AddTypeToSwagger<T>(this OpenApiDocumentGeneratorSettings settings)
    {
        settings.DocumentProcessors.Add(new TypeMapDocumentProcessor<T>());
    }
}

public class TypeMapDocumentProcessor<T> : IDocumentProcessor
{
    public void Process(DocumentProcessorContext context)
    {
        var schema = context.SchemaGenerator.Generate(typeof(T));
        context.Document.Definitions[typeof(T).Name] = schema;
    }
}

public class MakeAllPropertiesRequiredProcessor : IDocumentProcessor
{
    public void Process(DocumentProcessorContext context)
    {
        foreach (var schema in context.Document.Definitions.Values)
        foreach (var property in schema.Properties)
            schema.RequiredProperties.Add(property.Key);
    }
}

//
//
// /// <summary>
// /// Adds OpenAPI extension to operations marked with [EventSourceEndpoint]
// /// Marks them with x-event-source: true and x-event-type: "EventTypeName"
// /// </summary>
// public sealed class EventSourceEndpointOperationProcessor : IOperationProcessor
// {
//     public bool Process(OperationProcessorContext context)
//     {
//         var attribute = context.MethodInfo.GetCustomAttribute<EventSourceEndpointAttribute>();
//         if (attribute == null)
//             return true;
//
//         // Add OpenAPI extensions to mark this as an EventSource endpoint
//         context.OperationDescription.Operation.ExtensionData ??= new Dictionary<string, object>();
//         context.OperationDescription.Operation.ExtensionData["x-event-source"] = true;
//         context.OperationDescription.Operation.ExtensionData["x-event-type"] = attribute.EventType.Name;
//
//         return true;
//     }
// }
//
// /// <summary>
// /// Generates metadata for SSE events to enable DRY, generic client-side subscriptions
// /// Discovers event types from [EventSourceEndpoint] attributes
// /// Creates: SseEventType string literal union for TypeScript
// /// </summary>
// public sealed class AddSseEventMetadataProcessor : IDocumentProcessor
// {
//     public void Process(DocumentProcessorContext context)
//     {
//         // Find all controller methods marked with [EventSourceEndpoint]
//         var sseEventTypes = AppDomain.CurrentDomain.GetAssemblies()
//             .SelectMany(a =>
//             {
//                 try
//                 {
//                     return a.GetTypes();
//                 }
//                 catch
//                 {
//                     return Array.Empty<Type>();
//                 }
//             })
//             .Where(t => t.IsClass && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
//             .SelectMany(controller => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
//             .Select(method => method.GetCustomAttribute<EventSourceEndpointAttribute>())
//             .Where(attr => attr != null)
//             .Select(attr => attr!.EventType.Name)
//             .Distinct()
//             .OrderBy(name => name)
//             .ToArray();
//
//         if (sseEventTypes.Length == 0)
//             return; // No SSE endpoints found
//
//         // Generate string literal union of event type names for TypeScript
//         var eventTypesSchema = new JsonSchema
//         {
//             Type = JsonObjectType.String,
//             Description = "SSE Event Type Names (auto-generated from [EventSourceEndpoint] attributes)"
//         };
//
//         foreach (var typeName in sseEventTypes)
//         {
//             eventTypesSchema.Enumeration.Add(typeName);
//         }
//
//         context.Document.Definitions["SseEventType"] = eventTypesSchema;
//     }
// }