using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Testing;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestMethodNamingAnalyzer : DiagnosticAnalyzer
{
	private const string TestAttribute = "TUnit.Core.TestAttribute";

	private static readonly Regex Convention = new("^[A-Z][A-Za-z0-9]*(?:_[A-Z0-9][A-Za-z0-9]*)+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

	private static readonly DiagnosticDescriptor Rule = new(
		"SAM0001",
		"Test method name must follow Method_Scenario_Expectation",
		"Test method '{0}' must be named Method_Scenario_Expectation: two or three underscore-joined PascalCase segments",
		"Naming",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "See docs/architecture/testing.md for the naming convention."
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSymbolAction(Analyze, SymbolKind.Method);
	}

	private static void Analyze(SymbolAnalysisContext context)
	{
		var method = (IMethodSymbol)context.Symbol;
		if (!method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == TestAttribute))
		{
			return;
		}

		if (Convention.IsMatch(method.Name))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(Rule, method.Locations[0], method.Name));
	}
}
