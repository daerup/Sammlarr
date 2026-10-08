using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Usage;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Usage;

public class UsageFeedsCardTests : BunitContext
{
	private static readonly FeedId Shows = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Anime = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Paused = FeedId.From(Guid.NewGuid());

	private static readonly RunId AnimeRun = RunId.From(Guid.NewGuid());

	private static readonly RunId ShowsRun = RunId.From(Guid.NewGuid());

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(UsageSamples.Clock());
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Row_Clicked_PicksThatFeedWithItsBrandEdge()
	{
		var cut = RenderCard();

		await Row(cut, "Seasonal anime").ClickAsync();

		var picked = cut.FindAll("[data-testid=usage-feed].usage-feed-picked").Single();
		await Assert.That(picked.GetAttribute("data-feed")).IsEqualTo("Seasonal anime");
		await Assert
			.That(picked.GetAttribute("style"))
			.IsEqualTo($"--edge: {Services.GetRequiredService<MediaProviderBrandingService>().Color(FeedProvider.AniList.Service)}");
	}

	[Test]
	public async Task Render_ByDefault_PicksTheBusiestFeed()
	{
		var cut = RenderCard();

		await Assert.That(cut.FindAll("[data-testid=usage-feed].usage-feed-picked").Single().GetAttribute("data-feed")).IsEqualTo("Latest shows");
	}

	[Test]
	public async Task Arrow_OfAFeed_LeadsToItsRunHistory()
	{
		var cut = RenderCard();

		await Assert
			.That(Row(cut, "Latest shows").QuerySelector("[data-testid=usage-feed-runs]")!.GetAttribute("href"))
			.IsEqualTo($"/feeds/{UrlGuid.Encode(Shows.Value)}/runs");
	}

	[Test]
	public async Task Dot_Clicked_OpensThatRun()
	{
		var cut = RenderCard();

		await cut.InvokeAsync(() => cut.FindComponent<BbScatterChart>().Instance.OnDataPointClick.InvokeAsync(new ChartClickEventArgs { DataIndex = 1 }));

		await Assert
			.That(Services.GetRequiredService<NavigationManager>().Uri)
			.EndsWith($"/feeds/{UrlGuid.Encode(Shows.Value)}/runs/{UrlGuid.Encode(ShowsRun.Value)}");
	}

	[Test]
	public async Task Render_APausedFeedThatNeverRan_SaysSo()
	{
		var cut = RenderCard();

		await Assert.That(Row(cut, "Old list").QuerySelector("[data-testid=usage-feed-last]")!.TextContent.Trim()).IsEqualTo("Never ran");
	}

	[Test]
	public async Task Tab_SwitchedToUnmatched_MarksTheRunsThatCouldNotLookUpTitles()
	{
		var cut = RenderCard();

		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Unmatched per run").ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-feeds-legend]").TextContent).Contains("Some titles could not be looked up");
	}

	private static AngleSharp.Dom.IElement Row(IRenderedComponent<UsageFeedsCard> cut, string name)
	{
		return cut.FindAll("[data-testid=usage-feed]").Single(row => row.GetAttribute("data-feed") == name);
	}

	private IRenderedComponent<UsageFeedsCard> RenderCard()
	{
		IReadOnlyList<UsageFeedLine> feeds =
		[
			Line(Anime, "Seasonal anime", FeedProvider.AniList, AnimeRun, -5, 300),
			Line(Shows, "Latest shows", FeedProvider.Mdblist, ShowsRun, -2, 1200),
			new(Paused, "Old list", FeedProvider.Tmdb, false, new None(), [], new UsageFeedStats(0, 0, 0, 0, 0, 0, TimeSpan.Zero)),
		];
		return Render<UsageFeedsCard>(parameters => parameters.Add(card => card.Feeds, feeds).Add(card => card.Range, UsageRange.Day));
	}

	private static UsageFeedLine Line(FeedId feed, string name, FeedProvider provider, RunId id, int hours, long calls)
	{
		var run = new UsageRun(id, UsageSamples.To.AddHours(hours), TimeSpan.FromSeconds(4), 100, 2, 1, UsageSamples.Tally(answered: calls));
		return new UsageFeedLine(feed, name, provider, true, Some.Of(run.StartedAt), [run], new UsageFeedStats(calls, calls, calls / 100.0, 3, 0.03, 1, run.Took));
	}
}
