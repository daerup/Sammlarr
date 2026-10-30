using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class UnfoldRowTests : BunitContext
{
	private static readonly RenderFragment Summary = builder => builder.AddMarkupContent(0, "<span class=\"summary\">TMDB</span>");

	private static readonly RenderFragment Details = builder => builder.AddMarkupContent(0, "<p class=\"details\">key</p>");

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_PutsTheSummaryInTheTriggerWithItsColumns()
	{
		var cut = RenderRow(parameters => parameters);

		var trigger = cut.Find("button");
		await Assert.That(trigger.ClassList).Contains("grid-cols-[15px_1fr_auto]");
		await Assert.That(trigger.QuerySelector(".summary")!.TextContent).IsEqualTo("TMDB");
	}

	[Test]
	public async Task Render_WhenOpen_ShowsTheDetails()
	{
		var cut = RenderRow(parameters => parameters.Add(row => row.Open, true).Add(row => row.OpenChanged, _ => { }));

		await Assert.That(cut.Find("button").GetAttribute("aria-expanded")).IsEqualTo("true");
		await Assert.That(cut.Find(".details").TextContent).IsEqualTo("key");
	}

	[Test]
	public async Task Render_WithAnEdge_SetsTheEdgeColour()
	{
		var cut = RenderRow(parameters => parameters.Add(row => row.Edge, "#01b4e4"));

		await Assert.That(cut.Find(".unfold-row").GetAttribute("style")).IsEqualTo("--edge: #01b4e4;");
	}

	[Test]
	public async Task Render_WhenDivided_MarksTheRowAndKeepsItsAttributes()
	{
		var cut = RenderRow(parameters => parameters.Add(row => row.Divided, true).Add(row => row.Class, "person-row").AddUnmatched("data-person", "Ada"));

		var row = cut.Find(".unfold-row");
		await Assert.That(row.ClassList).Contains("divided");
		await Assert.That(row.ClassList).Contains("person-row");
		await Assert.That(row.GetAttribute("data-person")).IsEqualTo("Ada");
	}

	[Test]
	public async Task Click_OnTheTrigger_ReportsTheNewState()
	{
		bool? opened = null;
		var cut = RenderRow(parameters => parameters.Add(row => row.OpenChanged, open => opened = open));

		await cut.Find("button").ClickAsync(new());

		await Assert.That(opened).IsTrue();
	}

	private IRenderedComponent<UnfoldRow> RenderRow(Func<ComponentParameterCollectionBuilder<UnfoldRow>, ComponentParameterCollectionBuilder<UnfoldRow>> extra)
	{
		return Render<UnfoldRow>(parameters =>
			extra(parameters.Add(row => row.TriggerClass, "grid-cols-[15px_1fr_auto] py-3").Add(row => row.Summary, Summary).Add(row => row.Details, Details))
		);
	}
}
