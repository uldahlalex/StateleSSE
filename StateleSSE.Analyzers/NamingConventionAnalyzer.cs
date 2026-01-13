using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StateleSSE.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NamingConventionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SSE001";
    private static readonly LocalizableString Title = "SSE endpoint naming convention";
    private static readonly LocalizableString MessageFormat = "SSE endpoint '{0}' must contain 'Stream' in its name for CodeGen discovery";
    private static readonly LocalizableString Description = "SSE endpoints must contain 'Stream' in their method name. The TypeScript CodeGen tool relies on this convention to discover and generate client code for SSE endpoints.";
    private const string Category = "Naming";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var methodName = methodSymbol.Name;

        if (methodName.Contains("Stream"))
            return;

        // Check if it has EventSourceEndpoint attribute
        var hasEventSourceAttribute = methodSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "EventSourceEndpointAttribute");

        if (hasEventSourceAttribute)
        {
            var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodName);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
