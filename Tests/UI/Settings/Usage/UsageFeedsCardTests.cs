using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Usage;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Usage;

public class UsageFeedsCardTests : BunitContext
{
	private static readonly FeedId Shows = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Anime = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Paused = FeedId.From(Guid.NewGuid());

	private static readonly RunId ShowsRun = RunId.From(Guid.NewGuid());

	private static readonly RunId LimitedRun = RunId.From(Guid.NewGuid());

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(UsageSamples.Clock());
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

	private static AngleSharp.Dom.IElement Row(IRenderedComponent<Harness> cut, string name)
	{
		return cut.FindAll("[data-testid=usage-feed]").Single(row => row.GetAttribute("data-feed") == name);
	}

	private static AngleSharp.Dom.IElement Dot(IRenderedComponent<Harness> cut, RunId run)
	{
		return cut.FindAll("[data-testid=usage-feed-dot]").Single(dot => dot.GetAttribute("data-run") == run.ToString());
	}

	private static UsageRun Run(RunId id, int hours, UsageTally calls, int couldNotLookUp = 0)
	{
		return new UsageRun(id, UsageSamples.To.AddHours(hours), TimeSpan.FromSeconds(4), new Manual(), 100, 2, 1, 2, couldNotLookUp, calls);
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
			new UsageFeedStats(calls, calls, calls / 100.0, 3, 0.03, 1, TimeSpan.FromSeconds(4))
		);
	}

	private sealed class Harness : ComponentBase
	{
		private static readonly IReadOnlyList<UsageFeedLine> Feeds =
		[
			Line(
				Shows,
				"Latest shows",
				FeedProvider.Mdblist,
				Run(LimitedRun, -6, UsageSamples.Tally(answered: 900, after: 3)),
				Run(ShowsRun, -2, UsageSamples.Tally(answered: 1200))
			),
			Line(Anime, "Seasonal anime", FeedProvider.AniList, Run(RunId.From(Guid.NewGuid()), -5, UsageSamples.Tally(answered: 300), couldNotLookUp: 4)),
			new(Paused, "Old list", FeedProvider.Tmdb, false, new None(), [], new UsageFeedStats(0, 0, 0, 0, 0, 0, TimeSpan.Zero)),
		];

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageFeedsCard>(1);
			builder.AddComponentParameter(2, nameof(UsageFeedsCard.Feeds), Feeds);
			builder.AddComponentParameter(3, nameof(UsageFeedsCard.Range), UsageRange.Day);
			builder.AddComponentParameter(4, nameof(UsageFeedsCard.Window), new UsageWindow(UsageSamples.To.AddHours(-24), UsageSamples.To, TimeSpan.FromHours(1)));
			builder.CloseComponent();
		}
	}
}
