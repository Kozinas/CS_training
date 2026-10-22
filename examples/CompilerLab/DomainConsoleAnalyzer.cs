using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Course.CompilerLab;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainConsoleAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "COURSE001", "Console call in domain",
        "Move Console.{0} out of Course.Domain",
        "Architecture", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var console = start.Compilation.GetTypeByMetadataName("System.Console");
            if (console is null) return;
            start.RegisterOperationAction(operation =>
            {
                var invocation = (IInvocationOperation)operation.Operation;
                string ns = operation.ContainingSymbol.ContainingNamespace.ToDisplayString();
                if ((ns == "Course.Domain" || ns.StartsWith("Course.Domain.", StringComparison.Ordinal)) &&
                    SymbolEqualityComparer.Default.Equals(
                        invocation.TargetMethod.ContainingType, console))
                {
                    operation.ReportDiagnostic(Diagnostic.Create(
                        Rule, invocation.Syntax.GetLocation(), invocation.TargetMethod.Name));
                }
            }, OperationKind.Invocation);
        });
    }
}
