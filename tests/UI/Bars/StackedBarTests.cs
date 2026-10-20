using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TUnit.Assertions.Enums;
using UI.Shared.Bars;

namespace UI.Tests.Bars;

public class StackedBarTests : BunitContext
{
	private static readonly IReadOnlyList<Part> Run = [new("Already known", 206, "var(--usage-answered)"), new("Not found earlier", 6, "var(--fate-earlier)")];

	private readonly List<IReadOnlyList<string>> _hidden = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForEachPartWithAValue_GrowsBySize()
	{
		var cut = RenderBar([new("IMDb ratings", 3), new("Show parts", 0), new("Title ids", 5)]);

		await Assert.That(Names(cut)).IsEquivalentTo(["IMDb ratings", "Title ids"], CollectionOrdering.Matching);
		await Assert.That(Segments(cut)[1].GetAttribute("style")).StartsWith("flex: 5 1 0");
	}

	[Test]
	public async Task Render_WithATotalButNoRestName_EndsTheBarEarly()
	{
		var cut = RenderBar(Run, total: 424);

		await Assert.That(cut.Find(".sb-fill").GetAttribute("style")).StartsWith("width: 50%");
		await Assert.That(cut.FindAll("[data-part=rest]")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenThePartsPassTheTotal_FillTheBar()
	{
		var cut = RenderBar(Run, total: 100);

		await Assert.That(cut.Find(".sb-fill").GetAttribute("style")).StartsWith("width: 100%");
	}

	[Test]
	public async Task Tooltip_WithARestName_ListsWhatIsLeftLastAgainstTheTotal()
	{
		var cut = RenderBar(Run, total: 424, rest: "Still to come");

		var rows = await OpenTipAsync(cut);

		await Assert
			.That(rows.Select(Cells))
			.IsEquivalentTo(["Already known|206|48.6%", "Not found earlier|6|1.4%", "Still to come|212|50.0%"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Tooltip_PointedAtAPart_BoldsItsRowAndDimsTheOthers()
	{
		var cut = RenderBar(Run);

		await OpenTipAsync(cut);
		await Segments(cut)[1].TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var rows = cut.FindAll("[data-portal-content] [data-testid=stacked-bar-tip-row]");
		await Assert.That(rows.Select(row => row.GetAttribute("data-at") ?? string.Empty)).IsEquivalentTo(["false", "true"], CollectionOrdering.Matching);
		await Assert.That(Segments(cut).Select(segment => segment.ClassList.Contains("is-dim"))).IsEquivalentTo([true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Tooltip_PointedAtTheRest_BoldsItsRowAndDimsEverySegment()
	{
		var cut = RenderBar(Run, total: 424, rest: "Still to come");

		await OpenTipAsync(cut);
		await cut.Find("[data-part=rest]").TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var rows = cut.FindAll("[data-portal-content] [data-testid=stacked-bar-tip-row]");
		await Assert.That(rows.Select(row => row.GetAttribute("data-at") ?? string.Empty)).IsEquivalentTo(["false", "false", "true"], CollectionOrdering.Matching);
		await Assert.That(Segments(cut).Select(segment => segment.ClassList.Contains("is-dim"))).IsEquivalentTo([true, true], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Tooltip_HangsFromAnAnchorInsideTheTrackThatFollowsThePointer()
	{
		var cut = RenderBar(Run);

		await Assert.That(cut.FindAll("[data-testid=stacked-bar-track] [data-sb-anchor]")).HasSingleItem();
	}

	[Test]
	public async Task Tooltip_LeavingTheBar_ClosesIt()
	{
		var cut = RenderBar(Run);
		await OpenTipAsync(cut);

		await cut.Find("[data-testid=stacked-bar-track]").TriggerEventAsync("onmouseleave", new MouseEventArgs());

		cut.WaitForState(() => cut.FindAll("[data-portal-content][data-state=open]").Count == 0);
	}

	[Test]
	public async Task Tooltip_PointingAtALegendKey_StaysClosed()
	{
		var cut = RenderBar(Run, legend: true);

		await Key(cut, "Not found earlier").TriggerEventAsync("onmouseenter", new MouseEventArgs());
		await Task.Delay(TimeSpan.FromMilliseconds(300));

		await Assert.That(cut.FindAll("[data-portal-content][data-state=open]")).IsEmpty();
	}

	[Test]
	public async Task Tooltip_ForASinglePartWithoutATitle_IsNotOffered()
	{
		var cut = RenderBar([new("Feed editor", 4362)], total: 5000);

		await Assert.That(cut.FindComponents<BbTooltip>()).IsEmpty();
	}

	[Test]
	public async Task Tooltip_WithATitle_IsOfferedForASinglePartAndHeadsTheList()
	{
		var cut = RenderBar([new("Answered", 1396)], title: "TMDB");

		await OpenTipAsync(cut);

		await Assert.That(cut.Find("[data-portal-content] [data-testid=stacked-bar-tip]").TextContent).StartsWith("TMDB");
	}

	[Test]
	public async Task Tooltip_AfterHidingAPart_SharesOutOnlyWhatIsLeft()
	{
		var cut = RenderBar([new("d", 10), new("hdlists", 30), new("Added by hand", 60)], legend: true);

		await Key(cut, "Added by hand").ClickAsync(new());

		await Assert.That((await OpenTipAsync(cut)).Select(Cells)).IsEquivalentTo(["d|10|25.0%", "hdlists|30|75.0%"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Render_WhenAPartChangesItsValue_RedrawsTheBar()
	{
		var cut = RenderBar(Run);

		cut.FindComponent<StackedBar>()
			.Render(parameters => parameters.Add(bar => bar.ChildContent, Parts([new("Already known", 300), new("Not found earlier", 6)])));

		await Assert.That(Segments(cut)[0].GetAttribute("style")).StartsWith("flex: 300 1 0");
	}

	[Test]
	public async Task Legend_WhenAKeyIsClicked_HidesItsPartAndSaysWhichAreHidden()
	{
		var cut = RenderBar(Run, legend: true);

		await Key(cut, "Already known").ClickAsync(new());

		await Assert.That(Names(cut)).IsEquivalentTo(["Not found earlier"]);
		await Assert.That(Key(cut, "Already known").GetAttribute("aria-pressed")).IsEqualTo("false");
		await Assert.That(_hidden.Last()).IsEquivalentTo(["Already known"]);
	}

	[Test]
	public async Task Legend_WhenTheLastShownKeyIsClicked_KeepsItsPart()
	{
		var cut = RenderBar(Run, legend: true);

		await Key(cut, "Already known").ClickAsync(new());
		await Key(cut, "Not found earlier").ClickAsync(new());

		await Assert.That(Names(cut)).IsEquivalentTo(["Not found earlier"]);
	}

	[Test]
	public async Task Legend_WhenAHiddenKeyIsClickedAgain_ShowsItsPartOnceMore()
	{
		var cut = RenderBar(Run, legend: true);

		await Key(cut, "Already known").ClickAsync(new());
		await Key(cut, "Already known").ClickAsync(new());

		await Assert.That(Names(cut)).IsEquivalentTo(["Already known", "Not found earlier"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Legend_PointedAtAKey_DimsTheOtherSegments()
	{
		var cut = RenderBar(Run, legend: true);

		await Key(cut, "Not found earlier").TriggerEventAsync("onmouseenter", new MouseEventArgs());

		await Assert.That(Segments(cut).Select(segment => segment.ClassList.Contains("is-dim"))).IsEquivalentTo([true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Legend_WhenItIsNotAskedFor_IsNotDrawn()
	{
		var cut = RenderBar(Run);

		await Assert.That(cut.FindAll("[data-testid=stacked-bar-legend]")).IsEmpty();
	}

	[Test]
	public async Task Render_APartWithoutAColour_TurnsTheWheelFromTheNamedColourOnItsLeft()
	{
		var cut = RenderBar([new("IMDb ratings", 21, "#F5C518"), new("Anime ids", 3)]);

		await Assert.That(Segments(cut)[1].GetAttribute("style")).EndsWith("background: oklch(from #F5C518 var(--bar-auto-l) var(--bar-auto-c) calc(h + 137.5))");
	}

	private sealed record Part(string Name, long Value, string? Color = null);

	private static IReadOnlyList<IElement> Segments(IRenderedComponent<IComponent> cut) => cut.FindAll("[data-testid=stacked-bar-track] .sb-part");

	private static IEnumerable<string> Names(IRenderedComponent<IComponent> cut) =>
		Segments(cut).Select(segment => segment.GetAttribute("data-part") ?? string.Empty);

	private static IElement Key(IRenderedComponent<IComponent> cut, string name) => cut.Find($"[data-testid=stacked-bar-legend] [data-part='{name}']");

	private static string Cells(IElement row) => string.Join("|", row.Children.Select(cell => cell.TextContent.Trim()));

	private static async Task<IReadOnlyList<IElement>> OpenTipAsync(IRenderedComponent<IComponent> cut)
	{
		await Segments(cut)[0].TriggerEventAsync("onmouseenter", new MouseEventArgs());
		cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=stacked-bar-tip]");
		return cut.FindAll("[data-portal-content] [data-testid=stacked-bar-tip-row]");
	}

	private IRenderedComponent<IComponent> RenderBar(
		IReadOnlyList<Part> parts,
		long? total = null,
		string? rest = null,
		bool legend = false,
		string? title = null
	)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<StackedBar>(1);
			builder.AddComponentParameter(2, nameof(StackedBar.Total), total);
			builder.AddComponentParameter(3, nameof(StackedBar.RestName), rest);
			builder.AddComponentParameter(4, nameof(StackedBar.ShowLegend), legend);
			builder.AddComponentParameter(
				5,
				nameof(StackedBar.HiddenChanged),
				EventCallback.Factory.Create<IReadOnlySet<string>>(this, hidden => _hidden.Add([.. hidden]))
			);
			builder.AddComponentParameter(6, nameof(StackedBar.ChildContent), Parts(parts));
			if (title is not null)
			{
				builder.AddComponentParameter(7, nameof(StackedBar.Title), (RenderFragment)(content => content.AddContent(0, title)));
			}
			builder.CloseComponent();
		});
	}

	private static RenderFragment Parts(IReadOnlyList<Part> parts) =>
		content =>
		{
			foreach (var part in parts)
			{
				content.OpenComponent<StackedBarPart>(0);
				content.AddComponentParameter(1, nameof(StackedBarPart.Name), part.Name);
				content.AddComponentParameter(2, nameof(StackedBarPart.Value), part.Value);
				content.AddComponentParameter(3, nameof(StackedBarPart.Color), part.Color);
				content.CloseComponent();
			}
		};
}
