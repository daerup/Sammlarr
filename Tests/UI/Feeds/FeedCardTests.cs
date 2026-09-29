using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blacklist;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Feeds.Components;
using UI.Features.Feeds.Pages;
using UI.Shared.Services;
using UI.Shared.Services.Branding;
using UrlGuid = global::Shared.UrlGuid;

namespace UI.Tests.Feeds;

public class FeedCardTests : BunitContext
{
	private static readonly DateTimeOffset Now = new(2026, 9, 29, 18, 43, 0, TimeSpan.Zero);
	private static readonly FeedId Comedy = FeedId.From(new Guid(31, 0, 0, new byte[8]));

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(AsyncEnumerable.Empty<Result<IdentifiedMedia>>());
		Services.AddSingleton(media.Object);
		Services.AddSingleton(IWatchlistService.Mock().Object);
		Services.AddSingleton(IBlacklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForAPausedFeed_MarksTheCardAndSaysSo()
	{
		var cut = RenderCard(Row(enabled: false));

		await Assert.That(cut.Find("article").ClassList).Contains("is-paused");
		await Assert.That(cut.Find(".feed-card-corner").TextContent).Contains("Paused");
	}

	[Test]
	public async Task Render_ForAPausedFeed_DisablesItsRunButton()
	{
		var cut = RenderCard(Row(enabled: false));

		var run = cut.Find(".feed-card-run");
		await Assert.That(run.HasAttribute("disabled")).IsTrue();
		await Assert.That(run.GetAttribute("aria-label")).IsEqualTo("Paused: resume the feed to run it");
	}

	[Test]
	public async Task Render_ForAFeedWithHistory_LinksItsCountsToTheWatchlistAndTheRunHistory()
	{
		var cut = RenderCard(Row(owned: 1, runs: 79));

		var links = cut.FindAll(".feed-stat-link a").Select(link => link.GetAttribute("href") ?? string.Empty).ToList();
		await Assert.That(links).IsEquivalentTo([$"/watchlist?feed={UrlGuid.Encode(Comedy.Value)}", $"/feeds/{UrlGuid.Encode(Comedy.Value)}/runs"]);
	}

	[Test]
	public async Task Render_WithNothingOnTheWatchlist_ShowsAPlainZeroRatherThanALink()
	{
		var cut = RenderCard(Row(owned: 0, runs: 0));

		await Assert.That(cut.FindAll(".feed-stat-link a")).IsEmpty();
	}

	[Test]
	public async Task Render_ForAMirrorFeed_ShowsItsGraceAfterASeparator()
	{
		var cut = RenderCard(Row(mirror: 3));

		await Assert.That(Normalized(cut.Find(".feed-card-chips").TextContent)).StartsWith("Mirror · 3");
	}

	[Test]
	public async Task Render_WhileSelecting_TradesTheEditLinkAndRunButtonForASelectToggle()
	{
		var cut = RenderCard(Row(), selecting: true);

		await Assert.That(cut.FindAll(".feed-card-link")).IsEmpty();
		await Assert.That(cut.FindAll(".feed-card-run")).IsEmpty();
		await Assert.That(cut.Find(".feed-card-select").GetAttribute("aria-label")).IsEqualTo("Select hdlists/comedy-movies-2001-20207");
	}

	[Test]
	public async Task Render_WhileTheFeedRuns_DisablesItsRunButton()
	{
		var cut = RenderCard(Row(job: JobState.Running));

		var run = cut.Find(".feed-card-run");
		await Assert.That(run.HasAttribute("disabled")).IsTrue();
		await Assert.That(run.GetAttribute("aria-label")).IsEqualTo("Running now");
	}

	private IRenderedComponent<FeedCard> RenderCard(FeedRow row, bool selecting = false) =>
		Render<FeedCard>(parameters => parameters.Add(card => card.Row, row).Add(card => card.Now, Now).Add(card => card.IsSelecting, selecting));

	private static string Normalized(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	private static FeedRow Row(bool enabled = true, int? mirror = null, int owned = 0, int runs = 0, JobState? job = null) =>
		new(
			new Feed
			{
				Name = "hdlists/comedy-movies-2001-20207",
				Id = Comedy,
				Enabled = enabled,
				Provider = FeedProvider.Mdblist,
				Strategy = mirror is { } grace ? new FeedStrategy(new Mirror(grace)) : new FeedStrategy(new Append()),
				Identifier = "hdlists/comedy-movies-2001-2020",
				CreatedAt = Now,
				Schedule = new FeedRunTrigger(new Scheduled("0 26 * * * *")),
			},
			runs > 0 ? Overview(runs) : null,
			owned,
			job,
			enabled ? Now.AddMinutes(43) : null
		);

	private static FeedOverview Overview(int runs) =>
		new(
			new FeedRun
			{
				Id = Guid.NewGuid(),
				FeedId = Comedy,
				StartedAt = Now.AddMinutes(-16),
				Duration = TimeSpan.FromSeconds(2),
				Strategy = new FeedStrategy(new Append()),
				Trigger = new FeedRunTrigger(new Manual()),
				Conclusiveness = Conclusiveness.Conclusive,
				Counts = FeedRunCounts.None,
				Items = [],
			},
			new FeedRunHistory
			{
				TotalRuns = runs,
				FirstRunAt = Now.AddDays(-9),
				TotalAdded = 36,
				TotalRemoved = 6,
			}
		);
}
