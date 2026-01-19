using NJsonSchema.CodeGeneration.TypeScript;
using NJsonSchema.CodeGeneration.CSharp;
using NSwag;
using NSwag.CodeGeneration.TypeScript;
using NSwag.CodeGeneration.CSharp;
using NSwag.Generation;

namespace api.Etc;

public static class GenerateApiClientsExtensions
{
    public static async Task<string> GenerateApiClientsFromOpenApi(
        this WebApplication app,
        string clientOutputPath,
        string openApiOutputPath,
        string? csharpClientOutputPath = null)
    {
        var document = await app.Services.GetRequiredService<IOpenApiDocumentGenerator>()
            .GenerateAsync("v1");

        var openApiJson = document.ToJson();

        Directory.CreateDirectory(Path.GetDirectoryName(openApiOutputPath)!);
        await File.WriteAllTextAsync(openApiOutputPath, openApiJson);

        var documentFromJson = await OpenApiDocument.FromJsonAsync(openApiJson);

        var settings = new TypeScriptClientGeneratorSettings
        {
            Template = TypeScriptTemplate.Fetch,
            TypeScriptGeneratorSettings =
            {
                TypeStyle = TypeScriptTypeStyle.Interface,
                DateTimeType = TypeScriptDateTimeType.String,
                NullValue = TypeScriptNullValue.Undefined,
                TypeScriptVersion = 5.2m,
                GenerateCloneMethod = false,
                MarkOptionalProperties = true,
                GenerateConstructorInterface = true,
                ConvertConstructorInterfaceData = true
            }
        };

        var generator = new TypeScriptClientGenerator(documentFromJson, settings);
        var code = generator.GenerateFile();

        Directory.CreateDirectory(Path.GetDirectoryName(clientOutputPath)!);
        await File.WriteAllTextAsync(clientOutputPath, code);

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("OpenAPI JSON with documentation saved at: " + openApiOutputPath);
        logger.LogInformation("TypeScript client generated at: " + clientOutputPath);

        if (!string.IsNullOrEmpty(csharpClientOutputPath))
        {
            var csharpSettings = new CSharpClientGeneratorSettings
            {
                ClassName = "ChatApiClient",
                GenerateClientInterfaces = true,
                UseBaseUrl = true,
                InjectHttpClient = true,
                GenerateExceptionClasses = true,
                ExceptionClass = "ApiException",
                CSharpGeneratorSettings =
                {
                    Namespace = "ChatApi.Client",
                    JsonLibrary = CSharpJsonLibrary.SystemTextJson
                }
            };

            var csharpGenerator = new CSharpClientGenerator(documentFromJson, csharpSettings);
            var csharpCode = csharpGenerator.GenerateFile();

            Directory.CreateDirectory(Path.GetDirectoryName(csharpClientOutputPath)!);
            await File.WriteAllTextAsync(csharpClientOutputPath, csharpCode);

            logger.LogInformation("C# client generated at: " + csharpClientOutputPath);
        }

        return openApiOutputPath;
    }
}