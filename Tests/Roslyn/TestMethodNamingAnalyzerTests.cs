using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Testing;

namespace Roslyn.Tests;

public class TestMethodNamingAnalyzerTests
{
	private static readonly MetadataReference[] References =
	[
		.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
	];

	[Test]
	[Arguments("From_WhenEmptyGuid_Throws")]
	[Arguments("SendAsync_WhenUnauthorized_IsUnrecoverable")]
	[Arguments("Compute_ForAnyScenario_NeverPullsAnIdTwice")]
	[Arguments("Render_ForAnEnum_EmitsOneButtonPerValue")]
	public async Task Analyze_WhenTheNameFollowsTheConvention_ReportsNothing(string name)
	{
		await Assert.That(await DiagnoseAsync($"[Test] public void {name}() {{ }}")).IsEmpty();
	}

	[Test]
	[Arguments("A_cached_movie_resolves")]
	[Arguments("The_first_user_to_sign_in_becomes_an_admin")]
	[Arguments("NoSegmentsAtAll")]
	[Arguments("lowercaseStart_Scenario_Expectation")]
	[Arguments("Trailing_")]
	public async Task Analyze_WhenTheNameBreaksTheConvention_ReportsTheViolation(string name)
	{
		var diagnostics = await DiagnoseAsync($"[Test] public void {name}() {{ }}");

		await Assert.That(diagnostics).Count().IsEqualTo(1);
		await Assert.That(diagnostics[0].Id).IsEqualTo("SAM0001");
		await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
		await Assert.That(diagnostics[0].GetMessage()).Contains(name);
	}

	[Test]
	public async Task Analyze_WhenTheMethodIsNotATest_ReportsNothing()
	{
		await Assert.That(await DiagnoseAsync("public void a_plain_helper_method() { }")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WhenTheMethodIsALifecycleHook_ReportsNothing()
	{
		await Assert.That(await DiagnoseAsync("[Before(HookType.Test)] public void Background() { }")).IsEmpty();
	}

	private static async Task<ImmutableArray<Diagnostic>> DiagnoseAsync(string member)
	{
		var source = $$"""
			using TUnit.Core;

			public class Probe
			{
				{{member}}
			}
			""";

		var compilation = CSharpCompilation.Create(
			"Probe",
			[CSharpSyntaxTree.ParseText(source)],
			References,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		await Assert.That(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

		return await compilation.WithAnalyzers([new TestMethodNamingAnalyzer()]).GetAnalyzerDiagnosticsAsync();
	}
}
