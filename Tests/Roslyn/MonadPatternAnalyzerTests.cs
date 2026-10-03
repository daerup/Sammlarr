using Analyzers.Monads;
using Microsoft.CodeAnalysis;
using TUnit.Assertions.Enums;

namespace Roslyn.Tests;

public class MonadPatternAnalyzerTests
{
	private static readonly MonadPatternAnalyzer Testee = new();

	[Test]
	[Arguments("return x is Some<int>(var v) ? v : 0;", "Use Match, Map(…).GetValueOr(…) or ToNullable()")]
	[Arguments("return r is Success<int>(var v) ? v : 0;", "Use Match, Map(…).GetValueOr(…) or ToNullable()")]
	[Arguments("int? n = 1; Optional<int> o = n is { } p ? Some.Of(p) : new None(); return o;", "Use Optional.Of(…)")]
	[Arguments("int? n = 1; Optional<int> o = n.HasValue ? Some.Of(n.Value) : new None(); return o;", "Use Optional.Of(…)")]
	[Arguments("return x is Some<int> && flag;", "Use IsSome(), IsNone() or Match(p, () => false)")]
	[Arguments("return r is Error || flag;", "Use IsSuccess(), IsError() or Match(p, _ => false)")]
	[Arguments("return x switch { Some<int>(var v) => v, None => 0 };", "Use Match or Map(…).GetValueOr(…)")]
	[Arguments("return r switch { Success<int>(var v) => v, Error => 0 };", "Use Match or Map(…).GetValueOr(…)")]
	[Arguments("switch (x) { case Some<int>(var v): return v; default: return null; }", "Use Match or Map(…).GetValueOr(…)")]
	[Arguments("if (x is Some<int>(var v)) { A(); } return null;", "Use Apply, ApplyNone or Match")]
	[Arguments("if (r is Success<int>(var v)) { A(); } return null;", "Use Apply, ApplyError or Match")]
	[Arguments("foreach (var o in many) { if (o is None) { continue; } A(); } return null;", "Use IsNone() or IsSome()")]
	[Arguments("x.Apply(v => { A(); B(); }); return null;", "Use one chained call per step")]
	[Arguments("r.ApplyError(e => { A(); B(); }); return null;", "Use one chained call per step")]
	[Arguments("await foreach (var item in stream) { if (item is Success<int>(var v)) { A(); } } return null;", "Use WhereSuccessAwait()")]
	[Arguments("await foreach (var item in stream) { if (item is Error e) { A(); } } return null;", "Use Apply, ApplyError or Match")]
	[Arguments("return x is null;", "Use IsNone() or IsSome()")]
	[Arguments("return r is { };", "Use IsError() or IsSuccess()")]
	[Arguments("return many.OrderBy(o => o).ToList();", "Use .ToNullable() in the key")]
	[Arguments("return new[] { r }.OrderByDescending(o => o).ToList();", "Use .ToNullable() in the key")]
	[Arguments("return $\"{x}\";", "Use GetValueOr(…) or Match(…)")]
	[Arguments("return $\"{r}\";", "Use GetValueOr(…) or Match(…)")]
	[Arguments("return \"value: \" + x;", "Use GetValueOr(…) or Match(…)")]
	[Arguments("Optional<string> o = null!; return o;", "Use new None()")]
	[Arguments("Optional<int> o = default; return o;", "Use new None()")]
	[Arguments("return default(Optional<int>);", "Use new None()")]
	[Arguments("Optional<int> o = new(); return o;", "Use new None()")]
	[Arguments("x.Apply(v => new ValueTask()); return null;", "Use async x => await StepAsync(x)")]
	[Arguments("r.Apply(v => new ValueTask()); return null;", "Use async x => await StepAsync(x)")]
	public async Task Analyze_ForAHandReadMonad_ProposesTheHelper(string body, string proposal)
	{
		var diagnostics = await MonadCompilation.AnalyzeBodyAsync(Testee, body);

		await Assert.That(diagnostics).Count().IsEqualTo(1);
		await Assert.That(diagnostics[0].Id).IsEqualTo("SAM0002");
		await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
		await Assert.That(diagnostics[0].GetMessage()).StartsWith(proposal);
	}

	[Test]
	[Arguments("if (x is not Some<int>(var v)) { return null; } using var buffer = new System.IO.MemoryStream(); return v;")]
	[Arguments("if (x is not Some<int>(var v)) { return null; } try { return v; } finally { A(); }")]
	[Arguments("foreach (var o in many) { if (o is not Some<int>(var v)) { continue; } A(); } return null;")]
	[Arguments("foreach (var o in many) { if (o is not Some<int>(var v)) { A(); continue; } B(); } return null;")]
	[Arguments("Optional<int> o = DateTime.Now.DayOfWeek is DayOfWeek.Monday ? Some.Of(1) : new None(); return o;")]
	[Arguments("var list = new List<int>(); Optional<int> o = list is { Count: > 0 } ? Some.Of(list[0]) : new None(); return o;")]
	[Arguments("return x.Map(v => v + 1).GetValueOr(0);")]
	[Arguments("return r.Match(v => false, e => e is { Exception: InvalidOperationException });")]
	[Arguments("await x.Apply(v => Task.CompletedTask); return null;")]
	[Arguments("x.Apply(v => A()); return null;")]
	public async Task Analyze_ForAHelperChainOrAnAllowedGuard_ReportsNothing(string body)
	{
		await Assert.That(await MonadCompilation.AnalyzeBodyAsync(Testee, body)).IsEmpty();
	}

	[Test]
	public async Task Analyze_InRazorMarkup_AllowsACaseCheckAndATemplateButNotTextHandlersOrTheCodeBlock()
	{
		var diagnostics = await MonadCompilation.AnalyzeAsync(Testee, Component, "Probe_razor.g.cs");

		await Assert
			.That(diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line).Order())
			.IsEquivalentTo([15, 16, 22], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Analyze_InGeneratedCodeOutsideRazor_ReportsNothing()
	{
		await Assert.That(await MonadCompilation.AnalyzeAsync(Testee, Component, "Probe.g.cs")).IsEmpty();
	}

	private const string Component = """
		using System;
		using Shared;

		public class RenderTreeBuilder
		{
			public void AddContent(int sequence, object? textContent) { }
		}

		public class Probe
		{
			public Optional<int> X { get; set; } = new None();

			protected void BuildRenderTree(RenderTreeBuilder __builder)
			{
				if (X is Some<int>(var v)) { Console.WriteLine(v); }
				__builder.AddContent(1, X);
				Action handler = () => { if (X is Some<int>(var w)) { Console.WriteLine(w); } };
				Action<RenderTreeBuilder> template = builder => { if (X is Some<int>(var t)) { Console.WriteLine(t); } };
			}

			private void Code()
			{
				if (X is Some<int>(var v)) { Console.WriteLine(v); }
			}
		}
		""";
}
