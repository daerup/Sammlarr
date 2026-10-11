using System.Globalization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Icons.Lucide.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Usage;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Usage;

public class UsageFeedsCardTests : BunitContext
{
	private static readonly FeedId Shows = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Anime = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Paused = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Horror = FeedId.From(Guid.NewGuid());

	private static readonly RunId ShowsRun = RunId.From(Guid.NewGuid());

	private static readonly RunId LimitedRun = RunId.From(Guid.NewGuid());

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(At(UsageSamples.To));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ByDefault_PicksNoFeedAndShowsEveryOneInItsColour()
	{
		var cut = Render<Harness>();

		await Assert.That(cut.FindAll(".usage-feed-picked")).IsEmpty();
		await Assert.That(cut.FindAll("[data-testid=usage-feed-group]")).Count().IsEqualTo(2);
		await Assert.That(cut.FindAll(".usage-feed-dim")).IsEmpty();
	}

	[Test]
	public async Task Row_ClickedTwice_KeepsOnlyThatFeedThenShowsAllAgain()
	{
		var cut = Render<Harness>();

		await Row(cut, "Seasonal anime").ClickAsync();

		var picked = cut.FindAll(".usage-feed-picked").Single();
		await Assert.That(picked.GetAttribute("data-feed")).IsEqualTo("Seasonal anime");
		await Assert
			.That(picked.GetAttribute("style"))
			.IsEqualTo($"--edge: {Services.GetRequiredService<MediaProviderBrandingService>().Color(FeedProvider.AniList.Service)}");
		await Assert.That(cut.FindAll(".usage-feed-dim")).Count().IsEqualTo(1);

		await Row(cut, "Seasonal anime").ClickAsync();

		await Assert.That(cut.FindAll(".usage-feed-picked")).IsEmpty();
		await Assert.That(cut.FindAll(".usage-feed-dim")).IsEmpty();
	}

	[Test]
	public async Task Line_Clicked_KeepsOnlyItsFeed()
	{
		var cut = Render<Harness>();

		await cut.Find("[data-testid=usage-feed-line]").ClickAsync();

		await Assert.That(cut.FindAll(".usage-feed-picked").Single().GetAttribute("data-feed")).IsEqualTo("Latest shows");
	}

	[Test]
	public async Task Background_Clicked_ShowsEveryFeedAgain()
	{
		var cut = Render<Harness>();
		await Row(cut, "Seasonal anime").ClickAsync();

		await cut.Find("[data-testid=usage-feeds-chart]").ClickAsync();

		await Assert.That(cut.FindAll(".usage-feed-picked")).IsEmpty();
	}

	[Test]
	public async Task Dot_Clicked_OpensItsRunInAPopover()
	{
		var cut = Render<Harness>();

		await Dot(cut, ShowsRun).ClickAsync();

		var details = cut.WaitForElement("[data-portal-content] [data-testid=usage-run-details]");
		await Assert.That(details.TextContent).Contains("Latest shows");
		await Assert.That(details.QuerySelector("[data-testid=usage-run-calls]")!.TextContent).Contains("1,200 · 12.0 per title");
		await Assert
			.That(details.QuerySelectorAll("a").Select(link => link.GetAttribute("href")))
			.Contains($"/feeds/{UrlGuid.Encode(Shows.Value)}/runs/{UrlGuid.Encode(ShowsRun.Value)}");
		await Assert.That(cut.FindAll(".usage-feed-picked")).IsEmpty();
	}

	[Test]
	public async Task Dot_Clicked_AnchorsThePopoverWhereTheDotSits()
	{
		var cut = Render<Harness>();

		await Dot(cut, ShowsRun).ClickAsync();

		var anchor = cut.Find("[data-testid=usage-feed-anchor]");
		await Assert.That(anchor.GetAttribute("style")).StartsWith("left: calc(46px + (100% - 46px) * 0.6667); top: ");
		await Assert.That(anchor.QuerySelector("button[aria-haspopup]")).IsNotNull();
	}

	[Test]
	public async Task Render_RunsOfTheLastHoursOnly_StretchTheTimeScaleFromTheFirstRunToNow()
	{
		Services.AddSingleton(At(UsageSamples.To.AddHours(-1)));

		var cut = Render<Harness>();

		await Assert
			.That(cut.FindAll("[data-testid=usage-feed-dot] circle:last-child").Select(dot => dot.GetAttribute("cx")))
			.IsEquivalentTo(["0%", "80%", "20%"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Hover_WhileAFeedIsPicked_LightsUpAPointedRowsFeedButNothingPointedInTheChart()
	{
		var cut = Render<Harness>();
		await Assert.That(HoverRules(cut)).Contains("grayscale");

		await Row(cut, "Seasonal anime").ClickAsync();

		var rules = HoverRules(cut);
		await Assert.That(rules).DoesNotContain("grayscale");
		await Assert.That(rules).Contains(".usage-feeds:has(.usage-feed[data-f=f0]:hover) .usage-feed-group[data-f=f0] { filter: none; opacity: 1; }");
		await Assert.That(rules).DoesNotContain(":has([data-f=f0]:hover)");
	}

	[Test]
	public async Task Hover_AfterUnpickingFromTheRow_WaitsForThePointerToLeaveIt()
	{
		var cut = Render<Harness>();
		await Row(cut, "Seasonal anime").ClickAsync();

		await Row(cut, "Seasonal anime").ClickAsync();

		await Assert.That(HoverRules(cut)).DoesNotContain("grayscale");

		await Row(cut, "Seasonal anime").TriggerEventAsync("onmouseleave", new MouseEventArgs());

		await Assert.That(HoverRules(cut)).Contains("grayscale");
	}

	[Test]
	public async Task Dot_OfARunThatHitA429_WearsARingInTheLimitedColour()
	{
		var cut = Render<Harness>();

		var rings = cut.FindAll("[data-testid=usage-feed-ring]");

		await Assert.That(rings).Count().IsEqualTo(1);
		await Assert.That(Dot(cut, LimitedRun).QuerySelector("[data-testid=usage-feed-ring]")!.GetAttribute("stroke")).IsEqualTo("var(--usage-after-429)");
		await Assert.That(cut.Find("[data-testid=usage-feeds-legend]").TextContent).Contains("A ring: some calls were rate-limited").And.Contains("some gave up");
	}

	[Test]
	public async Task Tab_SwitchedToUnmatched_RingsTheRunsThatCouldNotLookUpTitles()
	{
		var cut = Render<Harness>();

		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Unmatched per run").ClickAsync();

		await Assert.That(cut.FindAll("[data-testid=usage-feed-ring]")).Count().IsEqualTo(1);
		await Assert.That(cut.Find("[data-testid=usage-feeds-legend]").TextContent).Contains("A ring: some titles could not be looked up");
	}

	[Test]
	public async Task Render_TheCallsScale_SpreadsEachPowerOfTenFromTheBottomToTheTop()
	{
		var cut = Render<Harness>();

		var ticks = cut.FindAll("[data-testid=usage-feeds-tick]").Select(tick => (tick.TextContent, tick.GetAttribute("style"))).ToList();

		await Assert
			.That(ticks)
			.IsEquivalentTo(
				[("1", "top: 163px"), ("10", "top: 122.5px"), ("100", "top: 82px"), ("1,000", "top: 41.5px"), ("10,000", "top: 1px")],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Arrow_OfAFeed_LeadsToItsRunHistory()
	{
		var cut = Render<Harness>();

		await Assert
			.That(Row(cut, "Latest shows").QuerySelector("[data-testid=usage-feed-runs]")!.GetAttribute("href"))
			.IsEqualTo($"/feeds/{UrlGuid.Encode(Shows.Value)}/runs");
	}

	[Test]
	public async Task Render_APausedFeedThatNeverRan_SaysSo()
	{
		var cut = Render<Harness>();

		await Assert.That(Row(cut, "Old list").QuerySelector("[data-testid=usage-feed-last]")!.TextContent.Trim()).IsEqualTo("Never ran");
	}

	[Test]
	public async Task Render_ADeletedFeed_StrikesItsNameThroughAndSaysWhenItWent()
	{
		var cut = Render<Harness>();

		var row = Row(cut, "Horror movies");
		await Assert.That(row.QuerySelector(".line-through")!.TextContent).IsEqualTo("Horror movies");
		await Assert
			.That(row.QuerySelector("[data-testid=usage-feed-deleted]")!.TextContent)
			.IsEqualTo($"deleted {UsageSamples.To.AddHours(-5).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)}");
		await Assert.That(row.Children[2].TextContent.Trim()).IsEqualTo("58");
		await Assert.That(row.QuerySelector("[data-testid=usage-feed-runs]")).IsNull();
		await Assert.That(row.QuerySelector("[data-testid=usage-feed-last]")).IsNull();
		await Assert.That(row.Children[0].QuerySelector("img")).IsNull();
		await Assert.That(row.Children[0].QuerySelector("path")!.GetAttribute("d")).IsEqualTo(Ghost());
	}

	[Test]
	public async Task Picked_AServiceADeletedFeedCalled_KeepsTheDeletedFeed()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Tmdb)));

		await Assert.That(cut.FindAll("[data-testid=usage-feed]").Select(row => row.GetAttribute("data-feed"))).IsEquivalentTo(["Latest shows", "Horror movies"]);
	}

	[Test]
	public async Task Picked_AService_KeepsOnlyTheFeedsThatCalledItAndCountsItsCalls()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Mdblist)));

		await Assert.That(cut.FindAll("[data-testid=usage-feed]").Select(row => row.GetAttribute("data-feed"))).IsEquivalentTo(["Latest shows"]);
		await Assert.That(Row(cut, "Latest shows").Children[2].TextContent.Trim()).IsEqualTo("1,103");
		await Assert.That(cut.FindAll("[data-testid=usage-feed-group]")).Count().IsEqualTo(1);
		await Assert.That(cut.Find("[data-testid=usage-pick-chip]").GetAttribute("data-service")).IsEqualTo("Mdblist");
		await Assert.That(cut.Find("[data-testid=usage-feeds]").TextContent).Contains("MDBList calls only, picked in the Calls table.");
	}

	[Test]
	public async Task Picked_AService_LeavesOutTheRunsThatNeverCalledIt()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Tmdb)));

		await Assert.That(cut.FindAll("[data-testid=usage-feed-dot]").Select(dot => dot.GetAttribute("data-run"))).IsEquivalentTo([ShowsRun.ToString()]);
	}

	[Test]
	public async Task Dot_ClickedWhileAServiceIsPicked_OpensTheRunWithItsCallsOnly()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Mdblist)));

		await Dot(cut, ShowsRun).ClickAsync();

		var details = cut.WaitForElement("[data-portal-content] [data-testid=usage-run-details]");
		await Assert.That(details.QuerySelector("[data-testid=usage-run-calls]")!.TextContent).Contains("200 · 2.0 per title");
	}

	[Test]
	public async Task Chip_Cleared_ShowsEveryServiceAgain()
	{
		Optional<RemoteService> raised = Some.Of(RemoteService.Mdblist);
		var cut = Render<Harness>(parameters =>
			parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Mdblist)).Add(harness => harness.PickedChanged, picked => raised = picked)
		);

		await cut.Find("[data-testid=usage-pick-chip] button[aria-label='Show every service']").ClickAsync();

		await Assert.That(raised).IsNone();
	}

	[Test]
	public async Task Picked_AServiceNoFeedCalled_SaysSo()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Plex)));

		await Assert.That(cut.Find("[data-testid=usage-feeds-none]").TextContent).IsEqualTo("No feed called Plex in the last 24 hours.");
		await Assert.That(cut.FindAll("[data-testid=usage-feed]")).IsEmpty();
	}

	[Test]
	public async Task Picked_AServiceThePickedFeedNeverCalled_DropsTheFeedsPick()
	{
		var cut = Render<Harness>();
		await Row(cut, "Seasonal anime").ClickAsync();

		cut.Render(parameters => parameters.Add(harness => harness.Picked, Some.Of(RemoteService.Mdblist)));
		cut.Render(parameters => parameters.Add(harness => harness.Picked, new None()));

		await Assert.That(cut.FindAll(".usage-feed-picked")).IsEmpty();
		await Assert.That(cut.FindAll(".usage-feed-dim")).IsEmpty();
	}

	private string? Ghost() =>
		Render<LucideIcon>(parameters => parameters.Add(icon => icon.Name, "ghost").Add(icon => icon.Size, 18)).Find("path").GetAttribute("d");

	private static AngleSharp.Dom.IElement Row(IRenderedComponent<Harness> cut, string name)
	{
		return cut.FindAll("[data-testid=usage-feed]").Single(row => row.GetAttribute("data-feed") == name);
	}

	private static string HoverRules(IRenderedComponent<Harness> cut) => cut.Find(".usage-feeds > style").TextContent;

	private static IDateTimeProvider At(DateTimeOffset now)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(now);
		return clock.Object;
	}

	private static AngleSharp.Dom.IElement Dot(IRenderedComponent<Harness> cut, RunId run)
	{
		return cut.FindAll("[data-testid=usage-feed-dot]").Single(dot => dot.GetAttribute("data-run") == run.ToString());
	}

	private static UsageRun Run(RunId id, int hours, UsageTally calls, IReadOnlyDictionary<RemoteService, UsageTally> services, int couldNotLookUp = 0)
	{
		return new UsageRun(id, UsageSamples.To.AddHours(hours), TimeSpan.FromSeconds(4), new Manual(), 100, 2, 1, 2, couldNotLookUp, calls, services);
	}

	private static UsageFeedLine Line(FeedId feed, string name, FeedProvider provider, params UsageRun[] runs)
	{
		var calls = runs.Sum(run => run.Calls.Total);
		return new UsageFeedLine(
			feed,
			name,
			provider,
			true,
			Some.Of(runs[^1].StartedAt),
			runs,
			Stats(calls),
			runs.SelectMany(run => run.Services.Keys)
				.Distinct()
				.ToDictionary(service => service, service => Stats(runs.Sum(run => run.Services.GetValueOrNone(service).Map(tally => tally.Total).GetValueOr(0))))
		);
	}

	private static UsageFeedStats Stats(long calls) => new(calls, calls, calls / 100.0, 3, 0.03, 1, TimeSpan.FromSeconds(4));

	private sealed class Harness : ComponentBase
	{
		private static readonly IReadOnlyList<UsageFeedLine> Feeds =
		[
			Line(
				Shows,
				"Latest shows",
				FeedProvider.Mdblist,
				Run(
					LimitedRun,
					-6,
					UsageSamples.Tally(answered: 900, after: 3),
					new Dictionary<RemoteService, UsageTally> { [RemoteService.Mdblist] = UsageSamples.Tally(answered: 900, after: 3) }
				),
				Run(
					ShowsRun,
					-2,
					UsageSamples.Tally(answered: 1200),
					new Dictionary<RemoteService, UsageTally>
					{
						[RemoteService.Mdblist] = UsageSamples.Tally(answered: 200),
						[RemoteService.Tmdb] = UsageSamples.Tally(answered: 1000),
					}
				)
			),
			Line(
				Anime,
				"Seasonal anime",
				FeedProvider.AniList,
				Run(
					RunId.From(Guid.NewGuid()),
					-5,
					UsageSamples.Tally(answered: 300),
					new Dictionary<RemoteService, UsageTally> { [RemoteService.AniList] = UsageSamples.Tally(answered: 300) },
					couldNotLookUp: 4
				)
			),
			new(
				Paused,
				"Old list",
				FeedProvider.Tmdb,
				false,
				new None(),
				[],
				new UsageFeedStats(0, 0, 0, 0, 0, 0, TimeSpan.Zero),
				new Dictionary<RemoteService, UsageFeedStats>()
			),
			new UsageFeedLine(
				Horror,
				"Horror movies",
				FeedProvider.Mdblist,
				false,
				new None(),
				[],
				Stats(58),
				new Dictionary<RemoteService, UsageFeedStats> { [RemoteService.Tmdb] = Stats(58) }
			)
			{
				Gone = Some.Of(
					new UsageFeedGone(UsageSamples.To.AddHours(-5), new Dictionary<RemoteService, UsageTally> { [RemoteService.Tmdb] = UsageSamples.Tally(answered: 58) })
				),
			},
		];

		[Parameter]
		public Optional<RemoteService> Picked { get; set; } = new None();

		[Parameter]
		public EventCallback<Optional<RemoteService>> PickedChanged { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageFeedsCard>(1);
			builder.AddComponentParameter(2, nameof(UsageFeedsCard.Feeds), Feeds);
			builder.AddComponentParameter(3, nameof(UsageFeedsCard.Range), UsageRange.Day);
			builder.AddComponentParameter(4, nameof(UsageFeedsCard.Window), new UsageWindow(UsageSamples.To.AddHours(-24), UsageSamples.To, TimeSpan.FromHours(1)));
			builder.AddComponentParameter(5, nameof(UsageFeedsCard.Picked), Picked);
			builder.AddComponentParameter(6, nameof(UsageFeedsCard.PickedChanged), PickedChanged);
			builder.CloseComponent();
		}
	}
}
