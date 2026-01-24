using System.Reflection;
using NJsonSchema;
using NSwag.Generation;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace server;

public class TypeMapDocumentProcessor<T> : IDocumentProcessor
{
    public void Process(DocumentProcessorContext context)
    {
        var schema = context.SchemaGenerator.Generate(typeof(T));
        context.Document.Definitions[typeof(T).Name] = schema;
    }
}

public static class OpenAPITools
{
    public static void AddTypeToSwagger<T>(this OpenApiDocumentGeneratorSettings settings)
    {
        settings.DocumentProcessors.Add(new TypeMapDocumentProcessor<T>());
    }


    /// <summary>
    ///     Adds string constants from a static class to OpenAPI schema
    ///     Usage: config.AddStringConstants(typeof(Channels));
    /// </summary>
    public static void AddStringConstants(this OpenApiDocumentGeneratorSettings settings, Type type)
    {
        var processorType = typeof(StringConstantsDocumentProcessor<>).MakeGenericType(type);
        var processor = (IDocumentProcessor)Activator.CreateInstance(processorType)!;
        settings.DocumentProcessors.Add(processor);
    }

    /// <summary>
    ///     Adds string constants from a static class to OpenAPI schema (generic version)
    ///     Usage: config.AddStringConstants&lt;Channels&gt;();
    /// </summary>
    public static void AddStringConstants<T>(this OpenApiDocumentGeneratorSettings settings) where T : class
    {
        settings.DocumentProcessors.Add(new StringConstantsDocumentProcessor<T>());
    }
}



/// <summary>
///     Document processor that extracts string constant values from a static class
///     and adds them to the OpenAPI schema as an object with string properties.
///     Supports: const fields, static readonly fields, and static properties.
/// </summary>
public class StringConstantsDocumentProcessor<T> : IDocumentProcessor where T : class
{
    public void Process(DocumentProcessorContext context)
    {
        var type = typeof(T);

        var schema = new JsonSchema
        {
            Type = JsonObjectType.Object,
            Description = $"String constants from {type.Name}",
            AdditionalPropertiesSchema = null,
            AllowAdditionalProperties = false
        };

        var constantValues = new Dictionary<string, string>();

        // Get const and static readonly string fields
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && (f.IsLiteral || f.IsInitOnly));

        foreach (var field in fields)
        {
            var value = field.GetValue(null) as string;
            if (value != null)
            {
                constantValues[field.Name] = value;
                schema.Properties[field.Name] = new JsonSchemaProperty
                {
                    Type = JsonObjectType.String,
                    IsReadOnly = true,
                    Default = value,
                    Description = $"Constant value: \"{value}\""
                };
            }
        }

        // Also get static string properties (for flexibility)
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(null) as string;
            if (value != null && !constantValues.ContainsKey(prop.Name))
            {
                constantValues[prop.Name] = value;
                schema.Properties[prop.Name] = new JsonSchemaProperty
                {
                    Type = JsonObjectType.String,
                    IsReadOnly = true,
                    Default = value,
                    Description = $"Constant value: \"{value}\""
                };
            }
        }

        schema.ExtensionData = new Dictionary<string, object?>
        {
            ["x-constant-values"] = constantValues
        };

        context.Document.Definitions[type.Name] = schema;
    }
}