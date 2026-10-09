using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class LibrarySourcesPanelTests : BunitContext
{
	private static readonly IReadOnlyList<SourceShare> Sources = [new("hdlists/comedy-movies-2001-20207", 1), new("Added by hand or from Plex", 203)];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForEachSource_GrowsTheTooltipTriggerThatIsTheActualFlexItem()
	{
		var cut = Render<Harness>();

		await Assert.That(Grows(cut)).IsEquivalentTo(["flex: 1", "flex: 203"]);
	}

	[Test]
	public async Task Render_ForEachSource_GivesItsSegmentAndKeyTheirOwnTone()
	{
		var cut = Render<Harness>();

		await Assert.That(cut.FindAll(".stack-part").Select(Tone)).IsEquivalentTo(["tone-0", "tone-1"]);
		await Assert.That(cut.FindAll(".key-dot").Select(Tone)).IsEquivalentTo(["tone-0", "tone-1"]);
	}

	[Test]
	public async Task Render_ForMoreSourcesThanTones_GivesTheAccentToTheFirstSourceOnly()
	{
		IReadOnlyList<SourceShare> seven = [.. Enumerable.Range(1, 7).Select(rank => new SourceShare($"feed {rank}", 8 - rank))];

		var cut = Render<LibrarySourcesPanel>(parameters => parameters.Add(panel => panel.Sources, seven).Add(panel => panel.Total, 28));

		await Assert
			.That(cut.FindAll(".key-dot").Select(Tone))
			.IsEquivalentTo(["tone-0", "tone-1", "tone-2", "tone-3", "tone-4", "tone-1", "tone-2"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Legend_WhenASourceIsClicked_HidesItsSegmentAndCountsOnlyWhatIsLeft()
	{
		var cut = Render<Harness>();

		await cut.FindAll(".key")[1].ClickAsync(new());

		await Assert.That(Grows(cut)).IsEquivalentTo(["flex: 1"]);
		await Assert.That(cut.FindAll(".key")[1].GetAttribute("aria-pressed")).IsEqualTo("false");
		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo($"{1:N0} of {204:N0} titles");
	}

	[Test]
	public async Task Legend_WhenAHiddenSourceIsClickedAgain_ShowsItOnceMore()
	{
		var cut = Render<Harness>();

		await cut.FindAll(".key")[1].ClickAsync(new());
		await cut.FindAll(".key")[1].ClickAsync(new());

		await Assert.That(Grows(cut)).IsEquivalentTo(["flex: 1", "flex: 203"]);
	}

	[Test]
	public async Task Legend_WhenTheLastShownSourceIsClicked_KeepsItShown()
	{
		var cut = Render<Harness>();

		await cut.FindAll(".key")[1].ClickAsync(new());
		await cut.FindAll(".key")[0].ClickAsync(new());

		await Assert.That(Grows(cut)).IsEquivalentTo(["flex: 1"]);
	}

	[Test]
	public async Task Tooltip_ForASegment_GivesItsShareOfWhatIsShownToOneDecimal()
	{
		var cut = Render<Harness>();

		await Assert.That(await HoverFirstSegmentAsync(cut)).Contains($"1 of {204:N0} ({1 / 204d:P1})");
	}

	[Test]
	public async Task Tooltip_AfterHidingASource_SharesOutOnlyWhatIsLeft()
	{
		var cut = Render<Harness>();

		await cut.FindAll(".key")[1].ClickAsync(new());

		await Assert.That(await HoverFirstSegmentAsync(cut)).Contains($"1 of 1 ({1d:P1})");
	}

	private static async Task<string> HoverFirstSegmentAsync(IRenderedComponent<Harness> cut)
	{
		await cut.Find(".stack-part").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());
		return cut.WaitForElement("[data-portal-content][data-state=open] [role=tooltip]").TextContent;
	}

	private static IEnumerable<string> Grows(IRenderedComponent<Harness> cut) =>
		cut.FindAll(".stack .stack-part").Select(part => part.ParentElement!.GetAttribute("style") ?? string.Empty);

	private static string Tone(IElement element) => element.ClassList.Single(name => name.StartsWith("tone-", StringComparison.Ordinal));

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<LibrarySourcesPanel>(1);
			builder.AddComponentParameter(2, nameof(LibrarySourcesPanel.Sources), Sources);
			builder.AddComponentParameter(3, nameof(LibrarySourcesPanel.Total), 204);
			builder.CloseComponent();
		}
	}
}
