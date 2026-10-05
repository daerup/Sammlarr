using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Tests;

internal static class GeneratorRun
{
	private static readonly CSharpParseOptions Preview = new(LanguageVersion.Preview);

	internal static async Task<string> OutputAsync(IIncrementalGenerator generator, string sample)
	{
		var compilation = CSharpCompilation.Create(
			"Sample",
			[CSharpSyntaxTree.ParseText(sample, Preview, "Sample.cs")],
			MonadCompilation.References,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
		);
		var driver = CSharpGeneratorDriver
			.Create([generator.AsSourceGenerator()], parseOptions: Preview)
			.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);

		await Assert.That(generated.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

		var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).OrderBy(source => source.HintName, StringComparer.Ordinal);
		return string.Join("\n", sources.Select(source => $"{source.HintName}\n{source.SourceText}"));
	}
}
