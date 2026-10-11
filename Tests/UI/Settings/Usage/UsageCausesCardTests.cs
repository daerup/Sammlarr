using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageCausesCardTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_AllCauses_PutsTheShareAnsweredFirstTryInTheHalfCircle()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await Assert.That(cut.Find("[data-testid=usage-gauge-centre]").TextContent).IsEqualTo("92.9%");
		await Assert.That(cut.FindAll("[data-testid=usage-gauge-segment]")).Count().IsEqualTo(5);
		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("All causes").And.Contains("140 calls");
	}

	[Test]
	public async Task Cause_Picked_RedrawsTheHalfCircleForIt()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("Search").And.Contains("14 calls");
		await Assert
			.That(cut.FindAll("[data-testid=usage-gauge-segment]").Select(segment => segment.GetAttribute("data-outcome")!))
			.IsEquivalentTo(["Answered", "Cancelled"]);
		await Assert.That(cut.Find("[data-testid=usage-gauge-centre]").TextContent).IsEqualTo("71.4%");
	}

	[Test]
	public async Task Outcome_WithoutCalls_IsGreyedAndHasNoShare()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		var failed = cut.FindAll("[data-testid=usage-outcome]").Single(row => row.TextContent.Contains("Failed"));
		await Assert.That(failed.GetAttribute("data-empty")).IsEqualTo("true");
		await Assert.That(failed.TextContent).DoesNotContain("%");
	}

	[Test]
	public async Task Cause_WithoutTrouble_SaysEveryCallWasAnswered()
	{
		var cut = RenderCard(Period(new UsageCauseLine(UsageCause.Browse, UsageSamples.Tally(answered: 75), [])));

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Browse")).ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-no-trouble]").TextContent).Contains("No trouble:").And.Contains("all 75 calls were answered.");
		await Assert.That(cut.FindAll("[data-testid=usage-outcome]")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Render_UnmarkedAndConnectionTestCalls_NamesThemWithAHint()
	{
		var cut = RenderCard(
			Period(
				new UsageCauseLine(UsageCause.Other, UsageSamples.Tally(answered: 25, cancelled: 2), []),
				new UsageCauseLine(UsageCause.KeyTest, UsageSamples.Tally(answered: 1), [])
			)
		);

		await Assert.That(cut.FindAll("[data-testid=usage-cause-hinted]").Select(name => name.TextContent)).IsEquivalentTo(["Untagged", "Connection tests"]);
	}

	[Test]
	public async Task Cause_PickedButAbsentFromTheNextWindow_FallsBackToAllCauses()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);
		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Search")).ClickAsync();

		cut.Render(parameters => parameters.Add(card => card.Period, UsageSamples.Snapshot().Previous));

		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("All causes");
	}

	[Test]
	public async Task Picked_AService_CountsOnlyItsCallsAndShowsItsChip()
	{
		var cut = RenderCard(TwoServices(), Some.Of(RemoteService.Plex));

		await Assert
			.That(cut.FindAll("[data-testid=usage-cause]").Select(row => row.QuerySelector("span")!.TextContent.Trim()))
			.IsEquivalentTo(["All causes", "Search"]);
		await Assert.That(cut.Find("[data-testid=usage-cause-picked]").TextContent).Contains("40 calls");
		await Assert.That(cut.Find("[data-testid=usage-pick-chip]").GetAttribute("data-service")).IsEqualTo("Plex");
		await Assert.That(cut.Find("[data-testid=usage-causes]").TextContent).Contains("Plex, picked in the Calls table.");
	}

	[Test]
	public async Task Chip_Cleared_ShowsEveryServiceAgain()
	{
		Optional<RemoteService> raised = Some.Of(RemoteService.Plex);
		var cut = RenderCard(TwoServices(), Some.Of(RemoteService.Plex), picked => raised = picked);

		await cut.Find("[data-testid=usage-pick-chip] button[aria-label='Show every service']").ClickAsync();

		await Assert.That(raised).IsNone();
	}

	[Test]
	public async Task Picked_AServiceWithoutCallsInTheWindow_SaysSo()
	{
		var cut = RenderCard(TwoServices(), Some.Of(RemoteService.Mdblist));

		await Assert.That(cut.Find("[data-testid=usage-causes-none]").TextContent).IsEqualTo("No MDBList calls in the last 24 hours.");
		await Assert.That(cut.FindAll("[data-testid=usage-cause]")).IsEmpty();
	}

	[Test]
	public async Task FeedEditor_Picked_OpensItsFeedsUnderItsRow()
	{
		var trending = new UsageFeedName(FeedId.From(Guid.NewGuid()), "Trending shows", FeedProvider.Mdblist);
		var cut = RenderCard(
			Period(
				new UsageCauseLine(UsageCause.FeedRun, UsageSamples.Tally(answered: 300), []),
				new UsageCauseLine(
					UsageCause.FeedEditor,
					UsageSamples.Tally(answered: 53),
					[new UsageCauseFeed(Some.Of(trending), 41), new UsageCauseFeed(new None(), 12)]
				)
			)
		);

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Feed editor")).ClickAsync();

		await Assert
			.That(cut.FindAll("[data-testid=usage-cause-feed]").Select(row => string.Join("|", row.Children.Select(cell => cell.TextContent.Trim()))))
			.IsEquivalentTo(["Trending shows||41|", "New feed, not saved yet, or deleted||12|"], CollectionOrdering.Matching);
		await Assert
			.That(cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Feed editor")).NextElementSibling!.GetAttribute("data-testid"))
			.IsEqualTo("usage-cause-feed");
	}

	[Test]
	public async Task FeedRuns_Picked_OpensNothingUnderTheirRow()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await cut.FindAll("[data-testid=usage-cause]").Single(row => row.TextContent.Contains("Feed runs")).ClickAsync();

		await Assert.That(cut.FindAll("[data-testid=usage-cause-feed]")).IsEmpty();
	}

	private static UsagePeriod TwoServices()
	{
		var tmdb = UsageSamples.Line(RemoteService.Tmdb, UsageSamples.Tally(answered: 95)) with
		{
			Causes =
			[
				new UsageCauseLine(UsageCause.FeedRun, UsageSamples.Tally(answered: 90), []),
				new UsageCauseLine(UsageCause.Search, UsageSamples.Tally(answered: 5), []),
			],
		};
		var plex = UsageSamples.Line(RemoteService.Plex, UsageSamples.Tally(answered: 40)) with
		{
			Causes = [new UsageCauseLine(UsageCause.Search, UsageSamples.Tally(answered: 40), [])],
		};
		return UsageSamples.Period(
			[tmdb, plex],
			[
				new UsageCauseLine(UsageCause.FeedRun, UsageSamples.Tally(answered: 90), []),
				new UsageCauseLine(UsageCause.Search, UsageSamples.Tally(answered: 45), []),
			]
		);
	}

	private static UsagePeriod Period(params UsageCauseLine[] causes)
	{
		return UsageSamples.Period([UsageSamples.Line(RemoteService.Tmdb, UsageTally.Sum(causes.Select(line => line.Tally)))], causes);
	}

	private IRenderedComponent<UsageCausesCard> RenderCard(
		UsagePeriod period,
		Optional<RemoteService>? picked = null,
		Action<Optional<RemoteService>>? changed = null
	)
	{
		return Render<UsageCausesCard>(parameters =>
			parameters
				.Add(card => card.Period, period)
				.Add(card => card.Range, UsageRange.Day)
				.Add(card => card.Picked, picked ?? new None())
				.Add(card => card.PickedChanged, changed ?? (_ => { }))
		);
	}
}
