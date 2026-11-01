using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components.List;
using UI.Features.Feeds.Pages;
using UI.Shared.Filters;

namespace UI.Tests.Filters;

public class FilterSheetTests : BunitContext
{
	private static readonly SourceOption Manual = new("manual", "Added by hand", new SourceChoice(SourceKind.Manual));

	private ListFilter? _applied;
	private bool _open = true;

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Show_CountsTheDraftAndCommitsIt()
	{
		var portal = RenderListSheet(ListFilter.None);

		await Chip(portal, "Movies").ClickAsync(new());

		await Assert.That(portal.Find("[data-dialog-commit]").TextContent.Trim()).IsEqualTo("Show 3 titles");
		await portal.Find("[data-dialog-commit]").ClickAsync(new());
		await Assert.That(_applied!.Type).IsEqualTo(MediaType.Movie);
		await Assert.That(_open).IsFalse();
	}

	[Test]
	public async Task Picks_ChangeOnlyTheDraftUntilShown()
	{
		var portal = RenderListSheet(ListFilter.None with { Sources = [Manual.Choice] }, [Manual]);

		await Chip(portal, "Added by hand").ClickAsync(new());
		await portal.FindAll("[role=radio]").Single(radio => radio.TextContent.Trim() == "Title A to Z").ClickAsync(new());
		await Chip(portal, "Any type").ClickAsync(new());

		await Assert.That(_applied).IsNull();
		await Assert.That(Chip(portal, "Added by hand").GetAttribute("aria-pressed")).IsEqualTo("false");
		await Assert.That(Chip(portal, "Any type").GetAttribute("aria-checked")).IsEqualTo("true");
		await portal.Find("[data-dialog-commit]").ClickAsync(new());
		await Assert.That(_applied!.Sources).IsEmpty();
		await Assert.That(_applied.Sort).IsEqualTo(ListSort.TitleAscending);
		await Assert.That(_applied.Type).IsNull();
	}

	[Test]
	public async Task ClearAll_ResetsTheDraftButKeepsTheSort()
	{
		var portal = RenderListSheet(ListFilter.None with { Type = MediaType.Show, Sort = ListSort.TitleAscending });

		await portal.FindAll("button").Single(button => button.TextContent.Trim() == "Clear all").ClickAsync(new());

		await Assert.That(Chip(portal, "Any type").GetAttribute("aria-checked")).IsEqualTo("true");
		await Assert.That(portal.Find(".filter-sheet-radio[aria-checked=true]").TextContent.Trim()).IsEqualTo("Title A to Z");
		await Assert.That(_applied).IsNull();
	}

	[Test]
	public async Task Search_CountsAsYouTypeAndCommitsWithTheRest()
	{
		var portal = RenderListSheet(ListFilter.None);

		await portal.Find("input[type=search]").InputAsync(new ChangeEventArgs { Value = "Dune" });

		await Assert.That(portal.Find("[data-dialog-commit]").TextContent.Trim()).IsEqualTo("Show 1 title");
		await Assert.That(_applied).IsNull();
		await portal.Find("[data-dialog-commit]").ClickAsync(new());
		await Assert.That(_applied!.Search).IsEqualTo("Dune");
		await Assert.That(_open).IsFalse();
	}

	[Test]
	public async Task Search_Clear_EmptiesTheDraft()
	{
		var portal = RenderListSheet(ListFilter.None with { Search = "da" });
		await Assert.That(portal.Find("input[type=search]").GetAttribute("value")).IsEqualTo("da");

		await portal.Find("[aria-label='Clear search']").ClickAsync(new());

		await Assert.That(portal.Find("input[type=search]").GetAttribute("value")).IsEqualTo(string.Empty);
		await portal.Find("[data-dialog-commit]").ClickAsync(new());
		await Assert.That(_applied!.Search).IsNull();
	}

	[Test]
	public async Task Groups_WithoutChoices_AreLeftOut()
	{
		var portal = RenderListSheet(ListFilter.None);

		await Assert.That(portal.FindAll(".filter-sheet-group").Select(group => group.TextContent)).IsEquivalentTo(["Sort", "Type"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task FeedSheet_ListsItsGroupsInOrderAndCountsFeeds()
	{
		var portal = Render<BbPortalHost>();
		Render<FeedFilterSheet>(parameters =>
			parameters
				.Add(sheet => sheet.OpenChanged, _ => { })
				.Add(sheet => sheet.Filter, FeedFilter.None)
				.Add(sheet => sheet.Providers, [new ProviderCount(FeedProvider.Mdblist, 2)])
				.Add(sheet => sheet.Count, _ => 1)
		);
		portal.WaitForElement(".filter-sheet-body");

		await Assert
			.That(portal.FindAll(".filter-sheet-group").Select(group => group.TextContent))
			.IsEquivalentTo(["Sort", "Status", "Trigger", "Strategy", "Provider"], CollectionOrdering.Matching);
		await Assert.That(portal.Find(".filter-sheet-chip-count").TextContent).IsEqualTo("2");
		await Assert.That(portal.Find("[data-dialog-commit]").TextContent.Trim()).IsEqualTo("Show 1 feed");
	}

	private IRenderedComponent<BbPortalHost> RenderListSheet(ListFilter filter, IReadOnlyList<SourceOption>? sources = null)
	{
		var portal = Render<BbPortalHost>();
		Render<ListFilterSheet>(parameters =>
			parameters
				.Add(sheet => sheet.OpenChanged, open => _open = open)
				.Add(sheet => sheet.Filter, filter)
				.Add(sheet => sheet.OnApply, applied => _applied = applied)
				.Add(sheet => sheet.Sources, sources ?? [])
				.Add(
					sheet => sheet.Count,
					draft =>
						draft.Search is not null ? 1
						: draft.Type == MediaType.Movie ? 3
						: 7
				)
		);
		portal.WaitForElement(".filter-sheet-body");
		return portal;
	}

	private static IElement Chip(IRenderedComponent<BbPortalHost> portal, string label) =>
		portal.FindAll(".filter-sheet-chip").Single(chip => chip.TextContent.Trim() == label);
}
