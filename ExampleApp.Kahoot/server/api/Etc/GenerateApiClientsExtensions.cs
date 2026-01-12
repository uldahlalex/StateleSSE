using NJsonSchema.CodeGeneration.TypeScript;
using NSwag;
using NSwag.CodeGeneration.TypeScript;

namespace api.Etc;

public static class GenerateApiClientsExtensions
{
    public static async Task GenerateApiClientsFromOpenApi(this WebApplication app, string path)
    {
        // Step 1: Fetch OpenAPI JSON from the endpoint (Microsoft.AspNetCore.OpenApi)
        using var client = new HttpClient();
        var addresses = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
        var openApiUrl = $"{addresses}/openapi/v1.json";

        string openApiJson;
        try
        {
            openApiJson = await client.GetStringAsync(openApiUrl);
        }
        catch (HttpRequestException ex)
        {
            var log = app.Services.GetRequiredService<ILogger<Program>>();
            log.LogError(ex, "Failed to fetch OpenAPI spec from {Url}", openApiUrl);
            throw;
        }

        // Step 2: Save the OpenAPI JSON with documentation
        var openApiPath = Path.Combine(Directory.GetCurrentDirectory(), "openapi-with-docs.json");
        await File.WriteAllTextAsync(openApiPath, openApiJson);

        // Step 3: Parse the document with NSwag for TypeScript generation
        var documentFromJson = await OpenApiDocument.FromJsonAsync(openApiJson);

        // Step 4: Generate TypeScript client from the parsed OpenAPI document
        var settings = new TypeScriptClientGeneratorSettings
        {
            Template = TypeScriptTemplate.Fetch,
             // = true,  // Enable JSDoc generation
            TypeScriptGeneratorSettings =
            {
                TypeStyle = TypeScriptTypeStyle.Interface,
                DateTimeType = TypeScriptDateTimeType.String,
                NullValue = TypeScriptNullValue.Undefined,
                TypeScriptVersion = 5.2m,
                GenerateCloneMethod = false,
                MarkOptionalProperties = true,
                GenerateConstructorInterface = true,
                ConvertConstructorInterfaceData = true,
                EnumStyle = TypeScriptEnumStyle.StringLiteral
            }
        };

        // Step 5: Generate TypeScript client from the parsed OpenAPI document
        var generator = new TypeScriptClientGenerator(documentFromJson, settings);
        var code = generator.GenerateFile();

        var outputPath = Path.Combine(Directory.GetCurrentDirectory() + path);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllTextAsync(outputPath, code);
        
            
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("OpenAPI JSON with documentation saved at: " + openApiPath);
        logger.LogInformation("TypeScript client generated at: " + outputPath);
    }
}