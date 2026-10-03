using Analyzers.Monads;
using Microsoft.CodeAnalysis;

namespace Roslyn.Tests;

public class MonadBranchAnalyzerTests
{
	private static readonly MonadBranchAnalyzer Testee = new();

	[Test]
	[Arguments("if (x.IsSome()) { A(); } else { B(); } return null;", "x.Apply(value => …).ApplyNone(() => …)")]
	[Arguments("if (x.IsSome()) { A(); } return null;", "write x.Apply(value => …)")]
	[Arguments("if (x.IsNone()) { B(); } return null;", "write x.ApplyNone(() => …)")]
	[Arguments("if (!x.IsSome()) { B(); } return null;", "write x.ApplyNone(() => …)")]
	[Arguments("if (x.IsSome() == false) { B(); } return null;", "write x.ApplyNone(() => …)")]
	[Arguments("if (x.IsNone()) { return null; } return 1;", "x.Map(value => …).GetValueOr(…) or x.Match(value => …, () => …)")]
	[Arguments("if (x.IsSome()) { return 1; } return null;", "x.Map(value => …).GetValueOr(…) or x.Match(value => …, () => …)")]
	[Arguments("if (x.IsNone()) { throw new InvalidOperationException(); } return null;", "write x.ApplyNone(() => …)")]
	[Arguments("if (x.Map(v => v > 0).GetValueOr(false)) { A(); } else { B(); } return null;", "x.When(…).Apply(value => …).ApplyNone(() => …)")]
	[Arguments("if (x.Map(v => v > 0).GetValueOr(true)) { A(); } return null;", "x.When(…).Apply(value => …).ApplyNone(() => …)")]
	[Arguments("if (r.IsSuccess()) { A(); } else { B(); } return null;", "r.Apply(value => …).ApplyError(error => …)")]
	[Arguments("return x.IsSome() ? 1 : 0;", "x.Match(value => …, () => …)")]
	[Arguments("return r.IsError() ? 0 : 1;", "r.Match(value => …, error => …)")]
	[Arguments("if (x.IsNone() && flag) { throw new InvalidOperationException(); } return null;", "with the other condition inside the step")]
	[Arguments("switch (1) { case 1: if (x.IsNone()) { break; } A(); break; } return null;", "write x.ApplyNone(() => …)")]
	public async Task Analyze_ForABranchOnAPresenceRead_ProposesTheChain(string body, string proposal)
	{
		var diagnostics = await MonadCompilation.AnalyzeBodyAsync(Testee, body);

		await Assert.That(diagnostics).Count().IsEqualTo(1);
		await Assert.That(diagnostics[0].Id).IsEqualTo("SAM0003");
		await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
		await Assert.That(diagnostics[0].GetMessage()).Contains(proposal);
	}

	[Test]
	[Arguments("var present = x.IsSome(); return present;")]
	[Arguments("return x.IsSome() && flag;")]
	[Arguments("if (Check(x.IsSome())) { A(); } return null;")]
	[Arguments("if (setting.GetValueOr(false)) { A(); } return null;")]
	[Arguments("foreach (var o in many) { if (o.IsNone()) { continue; } A(); } return null;")]
	[Arguments("foreach (var o in many) { if (o.IsSome()) { A(); continue; } B(); } return null;")]
	[Arguments("foreach (var o in many) { if (o.IsSome()) { return o; } } return null;")]
	[Arguments("while (x.IsSome()) { break; } return null;")]
	[Arguments("if (x.Match(v => v > 0, () => false)) { A(); } return null;")]
	[Arguments("return many.Where(o => o.IsSome()).ToList();")]
	public async Task Analyze_ForABoolThatIsTheAnswer_ReportsNothing(string body)
	{
		await Assert.That(await MonadCompilation.AnalyzeBodyAsync(Testee, body)).IsEmpty();
	}

	[Test]
	[Arguments("if (x.IsSome()) { A(); } return null;")]
	[Arguments("if (x.Map(v => v > 0).GetValueOr(false)) { A(); } return null;")]
	public async Task Analyze_ForAPresenceReadInAnIf_NamesTheReceiver(string body)
	{
		var diagnostics = await MonadCompilation.AnalyzeBodyAsync(Testee, body);

		await Assert.That(diagnostics.Single().GetMessage()).StartsWith("'x' is read as a bool only to choose a branch; write ");
	}

	[Test]
	public async Task Analyze_InRazorMarkup_ProposesAMarkupCaseCheckForABranchAndMatchForAValue()
	{
		var messages = (await MonadCompilation.AnalyzeAsync(Testee, Component, "Probe_razor.g.cs")).Select(diagnostic => diagnostic.GetMessage()).ToList();

		await Assert.That(messages).Contains(message => message.Contains("write @if (X is None)"));
		await Assert.That(messages).Contains(message => message.Contains("write X.Match(value => …, () => …)"));
	}

	private const string Component = """
		using System;
		using Shared;

		public class RenderTreeBuilder { }

		public class Probe
		{
			public Optional<int> X { get; set; } = new None();

			protected void BuildRenderTree(RenderTreeBuilder __builder)
			{
				if (X.IsNone()) { Console.WriteLine("none"); }
				Console.WriteLine(X.IsSome() ? "a" : "b");
			}
		}
		""";
}
