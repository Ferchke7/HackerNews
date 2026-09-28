using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HackerNews.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CleanArchitectureDependenciesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HN0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Forbidden architectural dependency",
        "Layer '{0}' must not depend on layer '{1}'",
        "CleanArchitecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Violation of Clean Architecture dependency rule: inner layers cannot depend on outer layers.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not IdentifierNameSyntax identifier || identifier.Parent is BaseNamespaceDeclarationSyntax)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
        if (symbol is null)
            return;

        var sourceNamespace = context.ContainingSymbol?.ContainingNamespace?.ToDisplayString();
        var targetNamespace = symbol is INamespaceSymbol ns 
            ? ns.ToDisplayString() 
            : symbol.ContainingNamespace?.ToDisplayString();

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        if (!DependencyValidator.IsForbiddenDependency(sourceNamespace, targetNamespace, options))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            identifier.GetLocation(),
            sourceNamespace,
            targetNamespace));
    }
}
