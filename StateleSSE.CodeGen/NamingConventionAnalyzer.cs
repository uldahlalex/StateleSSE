using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StateleSSE.CodeGen;

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

    public const string PathDiagnosticId = "SSE002";
    private static readonly LocalizableString PathTitle = "SSE endpoint path convention";
    private static readonly LocalizableString PathMessageFormat = "SSE endpoint path '{0}' must contain 'Stream' for CodeGen discovery";
    private static readonly LocalizableString PathDescription = "SSE endpoint paths marked with [SseEndpoint] must contain 'Stream'. The TypeScript CodeGen tool relies on this convention to discover and generate client code for SSE endpoints.";

    private static readonly DiagnosticDescriptor PathRule = new DiagnosticDescriptor(
        PathDiagnosticId, PathTitle, PathMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: PathDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, PathRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var methodName = methodSymbol.Name;

        if (methodName.Contains("Stream"))
            return;

        var hasSseEndpointAttribute = methodSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "SseEndpointAttribute" ||
                        attr.AttributeClass?.Name == "EventSourceEndpointAttribute");

        if (hasSseEndpointAttribute)
        {
            var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        for (int i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var parameter = methodSymbol.Parameters[i];
            var hasSseEndpointAttr = parameter.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "SseEndpointAttribute");

            if (!hasSseEndpointAttr)
                continue;

            if (i >= invocation.ArgumentList.Arguments.Count)
                continue;

            var argument = invocation.ArgumentList.Arguments[i];

            if (argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var value = literal.Token.ValueText.ToLower();

                if (!value.Contains("stream"))
                {
                    var diagnostic = Diagnostic.Create(PathRule, literal.GetLocation(), value);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
