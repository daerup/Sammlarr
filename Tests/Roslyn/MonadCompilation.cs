using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Tests;

internal static class MonadCompilation
{
	internal static readonly MetadataReference[] References =
	[
		.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
	];

	internal static async Task<ImmutableArray<Diagnostic>> AnalyzeBodyAsync(DiagnosticAnalyzer analyzer, string body)
	{
		return await AnalyzeAsync(analyzer, Usage(body), "Usage.cs");
	}

	internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(DiagnosticAnalyzer analyzer, string source, string path)
	{
		var compilation = CSharpCompilation.Create(
			"Usage",
			[CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path)],
			References,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
		);

		await Assert.That(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

		return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
	}

	private static string Usage(string body)
	{
		return $$"""
			using System;
			using System.Collections.Generic;
			using System.Linq;
			using System.Threading.Tasks;
			using Shared;

			public class Usage
			{
				public async Task<object?> Run(
					Optional<int> x,
					Result<int> r,
					IAsyncEnumerable<Result<int>> stream,
					IEnumerable<Optional<int>> many,
					Optional<bool> setting,
					bool flag)
				{
					await Task.Yield();
					{{body}}
				}

				private static void A() { }

				private static void B() { }

				private static bool Check(bool value) => value;
			}
			""";
	}
}
