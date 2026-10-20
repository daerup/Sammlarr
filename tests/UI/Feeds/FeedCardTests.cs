using BlazorBlueprint.Components;
using Bunit;
using Contracts;
using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Jobs;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Components;
using UI.Features.Feeds.Pages;
using UI.Shared.Common;
using UI.Shared.Services;

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
		media.GetDetailsAsync(Any(), Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
		Services.AddSingleton(media.Object);
		Services.AddSingleton(IWantlistService.Mock().Object);
		Services.AddSingleton(IBlocklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddScoped<ScrollService>();
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		clock.Zone.Getter.Returns(TimeZoneInfo.Utc);
		Services.AddSingleton(clock.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForAPausedFeed_SaysSo()
	{
		var cut = RenderCard(Row(enabled: false));

		await Assert.That(cut.Find(".feed-card-corner").TextContent).Contains("Paused");
	}

	[Test]
	public async Task Render_ForAFeedWithHistory_LinksItsCountsToTheWantlistAndTheRunHistory()
	{
		var cut = RenderCard(Row(onWantlist: 1, runs: 79));

		var links = cut.FindAll(".feed-stat-link a").Select(link => link.GetAttribute("href") ?? string.Empty).ToList();
		await Assert.That(links).IsEquivalentTo([$"/wantlist?feed={UrlGuid.Encode(Comedy.Value)}", $"/feeds/{UrlGuid.Encode(Comedy.Value)}/runs"]);
	}

	[Test]
	public async Task Render_WithNothingOnTheWantlist_ShowsAPlainZeroRatherThanALink()
	{
		var cut = RenderCard(Row(onWantlist: 0, runs: 0));

		await Assert.That(cut.FindAll(".feed-stat-link a")).IsEmpty();
	}

	[Test]
	public async Task Render_ForAMirrorFeed_ShowsItsGraceAfterASeparator()
	{
		var cut = RenderCard(Row(mirror: 3));

		await Assert.That(cut.Find(".feed-card-chips").TextContent.Words()).StartsWith("Mirror · 3");
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
	[Arguments(JobState.Running, false, "Running now", true)]
	[Arguments(JobState.Running, true, "Running now", true)]
	[Arguments(JobState.Queued, true, "Waiting in the queue", true)]
	[Arguments(JobState.Waiting, false, "Waiting in the queue", true)]
	[Arguments(JobState.Done, false, "Paused: resume the feed to run it", true)]
	[Arguments(null, false, "Paused: resume the feed to run it", true)]
	[Arguments(JobState.Done, true, "Run", false)]
	[Arguments(null, true, "Run", false)]
	public async Task Render_ForEachJobState_LabelsTheRunButtonAndDisablesItUnlessTheFeedCanRun(JobState? job, bool enabled, string label, bool disabled)
	{
		var cut = RenderCard(Row(enabled: enabled, job: job));

		var run = cut.Find(".feed-card-run");
		await Assert.That(run.GetAttribute("aria-label")).IsEqualTo(label);
		await Assert.That(run.HasAttribute("disabled")).IsEqualTo(disabled);
	}

	[Test]
	public async Task Render_WhileRunning_ShowsTheRunningPillAndNoRunningChip()
	{
		var live = ILiveRun.Mock();
		live.Tally.Getter.Returns(RunTally.Of(FateTally.None with { Started = 14, Matched = 12 }, new None(), 3000, new None(), Some.Of(400)));

		var cut = Render<FeedCard>(parameters =>
			parameters.Add(card => card.Row, Row(runs: 3, job: JobState.Running)).Add(card => card.Now, Now).Add(card => card.Running, Some.Of(live.Object))
		);

		await Assert.That(cut.Find("[data-testid=running-pill]").TextContent.Words()).IsEqualTo("Running · 12 / ~400");
		await Assert.That(cut.FindAll(".last-run")).IsEmpty();
		await Assert.That(cut.Find(".feed-card-chips").TextContent).DoesNotContain("Running");
	}

	[Test]
	public async Task Render_WhileQueued_KeepsTheQueuedChip()
	{
		var cut = RenderCard(Row(runs: 3, job: JobState.Queued));

		await Assert.That(cut.Find(".feed-card-chips").TextContent).Contains("Queued");
		await Assert.That(cut.FindAll("[data-testid=running-pill]")).IsEmpty();
	}

	private IRenderedComponent<FeedCard> RenderCard(FeedRow row, bool selecting = false) =>
		Render<FeedCard>(parameters => parameters.Add(card => card.Row, row).Add(card => card.Now, Now).Add(card => card.IsSelecting, selecting));

	private static FeedRow Row(bool enabled = true, int? mirror = null, int onWantlist = 0, int runs = 0, JobState? job = null)
	{
		var feed = TestFeeds.Feed(Comedy.Value, "hdlists/comedy-movies-2001-20207") with
		{
			Enabled = enabled,
			Strategy = mirror is { } grace ? new FeedStrategy(new Mirror(grace)) : new FeedStrategy(new Append()),
			Schedule = new FeedRunTrigger(new Scheduled("0 26 * * * *")),
		};
		return new(
			FeedOverview.Of(feed, Optional.When(runs > 0, () => Totals(feed, runs)), onWantlist),
			Optional.Of(job),
			Optional.When(enabled, () => Now.AddMinutes(43))
		);
	}

	private static FeedRunTotals Totals(Feed feed, int runs) =>
		new(
			TestFeeds.Run(feed, Guid.NewGuid()) with
			{
				StartedAt = Now.AddMinutes(-16),
				Duration = TimeSpan.FromSeconds(2),
			},
			new FeedRunHistory
			{
				TotalRuns = runs,
				FirstRunAt = Some.Of(Now.AddDays(-9)),
				TotalAdded = 36,
				TotalRemoved = 6,
			}
		);
}
