using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Domain.Processing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Feeds.Pages.Editor.Preview;

namespace UI.Tests.Feeds.Preview;

public class VerdictPopoversTests : BunitContext
{
	private static readonly Enrollment Wantlisted = new()
	{
		InWantlist = true,
		InBlocklist = false,
		Owned = false,
	};

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(FeedFilterStub.Shipped);
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Bypassed_WithoutALimit_ShowsNoSlotLine()
	{
		var tip = await OpenAsync<BypassedPopover>(parameters => parameters.Add(popover => popover.Enrollment, Wantlisted));

		await Assert.That(Parts(tip)).IsEquivalentTo(["Already on your wantlist", "So this feed does not import it again."]);
		await Assert.That(tip.QuerySelector(".verdict-popover-detail")).IsNull();
	}

	[Test]
	public async Task Bypassed_ForAPreservedTitle_SaysItDoesNotCount()
	{
		var tip = await OpenAsync<BypassedPopover>(parameters =>
			parameters.Add(popover => popover.Enrollment, Wantlisted).Add(popover => popover.Limited, true).Add(popover => popover.Counts, false)
		);

		await Assert.That(tip.QuerySelector(".verdict-popover-detail")!.TextContent.Words()).IsEqualTo("Counts toward your limit No");
	}

	[Test]
	public async Task NotFound_ShowsTheIdsItWasLookedUpBy()
	{
		var unmatched = new UnmatchedMedia([TmdbId.From("259140", MediaType.Show)], new ShowDetails { Title = "Kusuriya no Hitorigoto" });

		var tip = await OpenAsync<NotFoundPopover>(parameters => parameters.Add(popover => popover.Unmatched, unmatched));

		await Assert.That(Parts(tip)).IsEquivalentTo(["Not found", "No service had this title.", "It is looked up again after an hour."]);
		await Assert.That(tip.QuerySelector(".verdict-popover-detail")!.TextContent.Words()).StartsWith("Looked up by").And.EndsWith("259140");
	}

	[Test]
	public async Task NotFound_WithoutIds_ShowsNoLookup()
	{
		var unmatched = new UnmatchedMedia([], new ShowDetails { Title = "Kusuriya no Hitorigoto" });

		var tip = await OpenAsync<NotFoundPopover>(parameters => parameters.Add(popover => popover.Unmatched, unmatched));

		await Assert.That(tip.QuerySelector(".verdict-popover-detail")).IsNull();
	}

	[Test]
	public async Task Truncated_ForARandomPick_SaysItsPlaceInLineAndThatTheNextRunDrawsAgain()
	{
		var tip = await OpenAsync<TruncatedPopover>(parameters =>
			parameters
				.Add(popover => popover.Rank, new SlotRank(86, 20))
				.Add(popover => popover.LastIn, "Firefly Wedding")
				.Add(popover => popover.Title, "Witch Hat Atelier")
				.Add(popover => popover.Order, "drawn at random")
				.Add(popover => popover.Random, true)
		);

		await Assert.That(Parts(tip)).IsEquivalentTo(["Past the import limit", "86th in line for the 20 slots, drawn at random.", "The next run draws again."]);
	}

	[Test]
	public async Task Culled_ByARuleAndAGroup_SaysTwoPlaces()
	{
		var tip = await OpenAsync<CulledPopover>(parameters =>
			parameters
				.Add(popover => popover.Filter, CulledDetailTests.VotedShortShowsOrMovies)
				.Add(
					popover => popover.Checks,
					new FilterChecks(
						[CulledDetailTests.Votes, CulledDetailTests.FewEpisodes, CulledDetailTests.IsShow, CulledDetailTests.IsMovie],
						[CulledDetailTests.Votes, CulledDetailTests.FewEpisodes, CulledDetailTests.IsMovie]
					)
				)
				.Add(popover => popover.Row, CulledDetailTests.Show(episodes: 1219, votes: 2))
				.Add(popover => popover.Strategy, new RatingStrategy(AveragedRating.Everything))
		);

		await Assert.That(tip.QuerySelector(".verdict-popover-title")!.TextContent).IsEqualTo("Filtered out in 2 places");
	}

	[Test]
	public async Task Leaving_ForOneOfTwo_SaysMissedOneOfTwoRunsAndFillsHalfTheEdge()
	{
		var tip = await OpenAsync<LeavingIndicator>(parameters =>
			parameters
				.Add(indicator => indicator.Reason, "It no longer matches your filter rules.")
				.Add(indicator => indicator.Threshold, 2)
				.Add(indicator => indicator.Absences, 1)
		);

		await Assert.That(tip.QuerySelector(".verdict-popover-head")!.TextContent.Words()).IsEqualTo("On its way out missed 1 of 2 runs");
		await Assert.That(tip.QuerySelector(".verdict-popover-edge i")!.GetAttribute("style")).IsEqualTo("width: 50%");
	}

	[Test]
	public async Task Leaving_WithADetail_ShowsItUnderTheReason()
	{
		var tip = await OpenAsync<LeavingIndicator>(parameters =>
			parameters
				.Add(indicator => indicator.Reason, "Your import limit no longer reaches it.")
				.Add(indicator => indicator.Threshold, 2)
				.Add(indicator => indicator.ChildContent, builder => builder.AddContent(0, "the line"))
		);

		await Assert.That(tip.QuerySelector(".verdict-popover-detail")!.TextContent).IsEqualTo("the line");
	}

	private async Task<IElement> OpenAsync<TPopover>(Action<ComponentParameterCollectionBuilder<TPopover>> parameters)
		where TPopover : IComponent
	{
		var portal = Render<BbPortalHost>();
		var cut = Render(parameters);
		await cut.Find(".search-result-status-indicator").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());
		return portal.WaitForElement("[data-portal-content][data-state=open] [role=tooltip]");
	}

	private static IEnumerable<string> Parts(IElement tip) => tip.QuerySelectorAll("p").Select(part => part.TextContent);
}
