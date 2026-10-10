using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts;
using Contracts.Announcing;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Providers;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Components;
using UI.Shared.Services;
using FeedsPage = UI.Features.Feeds.Pages.Feeds;

namespace UI.Tests.Feeds;

public class FeedsPageTests : BunitContext
{
	private static readonly FeedId Comedy = FeedId.From(new Guid(31, 0, 0, new byte[8]));

	private static readonly FeedId Drama = FeedId.From(new Guid(32, 0, 0, new byte[8]));

	private static readonly DateTimeOffset Start = new(2026, 10, 8, 18, 0, 0, TimeSpan.Zero);

	private readonly Mock<IDateTimeProvider> _clock = IDateTimeProvider.Mock();

	private readonly Mock<IJobQueue> _jobs = IJobQueue.Mock();

	private readonly Mock<IFeedRunPlanner> _planner = IFeedRunPlanner.Mock();

	[Test]
	public async Task RunFeeds_FromThePageHeader_OpensTheDialogWithItsOwnPicks()
	{
		RegisterServices([Overview()]);
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll(".feed-card").Count == 1);

		await Buttons(cut, "Run feeds").Single().ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".run-title").Count == 1);
		await Assert.That(cut.FindAll(".run-from")).IsEmpty();
	}

	[Test]
	public async Task RunFeeds_WhileBulkEditing_IsHidden()
	{
		RegisterServices([Overview()]);
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll(".feed-card").Count == 1);

		await Buttons(cut, "Bulk edit").Single().ClickAsync(new());

		await Assert.That(Buttons(cut, "Run feeds")).IsEmpty();
	}

	[Test]
	public async Task RunFeeds_WithoutAnyFeed_IsNotOffered()
	{
		RegisterServices([]);
		var cut = RenderPage();
		cut.WaitForState(() => cut.Markup.Contains("No feeds yet."));

		await Assert.That(Buttons(cut, "Run feeds")).IsEmpty();
	}

	[Test]
	public async Task Render_WhileYourFeedRuns_ShowsTheLiveView()
	{
		RegisterServices([Overview()], Some.Of(Comedy), Snapshot(new None()));
		var cut = RenderPage();

		cut.WaitForState(() => cut.FindAll("[data-testid=feed-run-live]").Count == 1);
		cut.WaitForState(() => cut.FindAll("[data-testid=running-pill]").Count == 1);
		await Assert.That(cut.Find("[data-testid=feed-run-live]").GetAttribute("aria-label")).IsEqualTo("Live run of Comedy");
		await Assert.That(cut.Find(".feed-card-chips").TextContent).DoesNotContain("Running");
	}

	[Test]
	public async Task Ended_BeforeTheFold_KeepsTheLiveView()
	{
		RegisterServices([Overview()], Some.Of(Comedy), Snapshot(Some.Of(new FeedRunEnd(TraceEnding.Completed, 900))));
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll("[data-testid=live-done]").Count == 1);

		await Task.Delay(700);

		cut.WaitForState(() => cut.FindAll("[data-testid=feed-run-live]").Count == 1);
		await Assert.That(cut.Find("[data-testid=running-pill]").TextContent).Contains("Done");
	}

	[Test]
	public async Task Fold_WhenANextRunStarts_ShowsTheNextRun()
	{
		RegisterServices([Overview(), Overview(Drama, "Drama")], Some.Of(Comedy), Snapshot(Some.Of(new FeedRunEnd(TraceEnding.Completed, 900))));
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll("[data-testid=live-done]").Count == 1);

		Services
			.GetRequiredService<IEventPublisher>()
			.Publish(new JobStateChangedEvent(TestCurrentUser.DefaultId, JobKind.FeedRun, Drama.Value.ToString(), JobState.Running));

		cut.WaitForState(() => cut.Find("[data-testid=feed-run-live]").GetAttribute("aria-label") == "Live run of Drama");
		await Assert.That(cut.FindAll("[data-testid=running-pill]")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task OpenLive_FromTheRunningPill_ScrollsToTheLiveView()
	{
		RegisterServices([Overview()], Some.Of(Comedy), Snapshot(new None()));
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll("[data-testid=running-pill]").Count == 1);

		await cut.Find("[data-testid=live-toggle]").ClickAsync(new());
		cut.WaitForState(() => cut.Find("[data-testid=live-more]").HasAttribute("hidden"));

		await cut.Find("[data-testid=running-pill]").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".live-flash").Count == 1);
		await Assert.That(cut.Find("[data-testid=live-more]").HasAttribute("hidden")).IsFalse();
		await Assert.That(JSInterop.Invocations["sammlarrScroll.intoView"].Single().Arguments.Single()).IsEqualTo("feed-run-live");
	}

	[Test]
	public async Task Fold_AfterTheRun_RemovesTheViewAndFlashesTheCard()
	{
		RegisterServices([Overview()], Some.Of(Comedy), Snapshot(Some.Of(new FeedRunEnd(TraceEnding.Completed, 900))));
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll("[data-testid=live-done]").Count == 1);

		_clock.Now.Getter.Returns(Start.AddSeconds(6));

		cut.WaitForState(() => cut.FindAll("[data-testid=feed-run-live]").Count == 0, TimeSpan.FromSeconds(3));
		await Assert.That(cut.Find(".feed-card-foot").ClassList).Contains("is-new");
		await Assert.That(cut.FindAll("[data-testid=running-pill]")).IsEmpty();
		await Assert.That(cut.Find("[data-testid=feed-run-idle]").TextContent).Contains("Nothing is running");
	}

	[Test]
	public async Task Render_WithFeedsAndNothingRunning_ShowsTheLiveViewSayingNothingIsRunning()
	{
		RegisterServices([Overview()]);

		var cut = RenderPage();

		cut.WaitForState(() => cut.FindAll("[data-testid=feed-run-idle]").Count == 1);
		await Assert.That(cut.Find("#feed-run-live").TextContent).Contains("Live view").And.Contains("Nothing is running");
		await Assert.That(cut.FindAll("[data-testid=feed-run-live]")).IsEmpty();
	}

	[Test]
	public async Task Render_WhileTheRunningFeedStillLoads_ShowsNoIdleLine()
	{
		RegisterServices([Overview()], Some.Of(Comedy), Snapshot(new None()));
		var layouts = IFeedRunLayout.Mock();
		layouts.GetAsync(Any<FeedId>(), Any()).ReturnsAsync(new TaskCompletionSource<RunLayout>().Task);
		Services.AddSingleton(layouts.Object);

		var cut = RenderPage();

		cut.WaitForState(() => cut.FindAll(".feed-card-foot").Count == 1);
		await Assert.That(cut.FindAll("#feed-run-live")).IsEmpty();
	}

	[Test]
	public async Task Render_WithoutFeeds_ShowsNoLiveView()
	{
		RegisterServices([]);

		var cut = RenderPage();

		cut.WaitForState(() => cut.Markup.Contains("No feeds yet."));
		await Assert.That(cut.FindAll("#feed-run-live")).IsEmpty();
	}

	[Test]
	public async Task Run_WhileThePreviewIsStillLoading_QueuesTheFeed()
	{
		RegisterServices([Overview()]);
		_planner.PlanAsync(Any<FeedPlanTarget>(), Any()).ReturnsAsync(new TaskCompletionSource<FeedPlanResult>().Task);
		_jobs.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Queued));
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll(".feed-card-run").Count == 1);
		_ = cut.Find(".feed-card-run").ClickAsync(new());
		cut.WaitForState(() => Buttons(cut, "Run").Any(button => button.TextContent.Trim() == "Run"));
		var run = Buttons(cut, "Run").Single(button => button.TextContent.Trim() == "Run");
		await Assert.That(run.HasAttribute("disabled")).IsFalse();
		await Assert.That(cut.Find("[role=dialog]").TextContent).Contains("Run \"Comedy\"?");

		await run.ClickAsync(new());

		_jobs.EnqueueAsync(Any<IJobRequest>(), Any()).WasCalled(Times.Once);
	}

	private void RegisterServices(IReadOnlyList<FeedOverview> overviews)
	{
		RegisterServices(overviews, new None(), new NoTrace());
	}

	private void RegisterServices(IReadOnlyList<FeedOverview> overviews, Optional<FeedId> running, TraceRead read)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		AddAuthorization().SetAuthorized("ana");
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());

		var feeds = IFeedService.Mock();
		feeds.GetAsync(Any()).Returns(new FeedSnapshot(overviews));
		feeds.GetRunsAsync(Any(), Any()).Returns([]);
		Services.AddSingleton(feeds.Object);

		var queue = IJobQueueView.Mock();
		IReadOnlyList<PendingJob> jobs =
		[
			.. new[] { running }
				.WhereSome()
				.Select(feed => new PendingJob(JobId.From(Guid.NewGuid()), new RunFeed(feed, new Manual()), JobState.Running, DateTimeOffset.UtcNow, 1)),
		];
		queue.GetAsync(Any()).Returns(new QueueSnapshot(jobs, 0));
		Services.AddSingleton(queue.Object);

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
		Services.AddSingleton(media.Object);

		Services.AddSingleton(_jobs.Object);
		Services.AddSingleton(_planner.Object);
		Services.AddSingleton(IWantlistService.Mock().Object);
		Services.AddSingleton(IBlocklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddSingleton<IEventPublisher>(new TestEventPublisher());
		Services.AddScoped<Notifier>();
		Services.AddScoped<ProfileDropdownService>();
		Services.AddScoped<AccountConnectionPrompt>();
		Services.AddProviderBranding();
		Services.AddScoped<ScrollService>();

		var traces = IFeedRunTraces.Mock();
		traces.ReadAsync(Any<FeedId>(), Any<TraceCursor>(), Any()).Returns(read);
		Services.AddSingleton(traces.Object);
		var layouts = IFeedRunLayout.Mock();
		layouts.GetAsync(Any<FeedId>(), Any()).Returns(RunLayout.Empty);
		Services.AddSingleton(layouts.Object);
		var capacity = IRemoteCapacity.Mock();
		capacity.Of(Any<RemoteService>()).Returns(new RemoteLoad(new None(), 0));
		Services.AddSingleton(capacity.Object);
		_clock.Now.Getter.Returns(Start);
		Services.AddSingleton(_clock.Object);
		Services.AddScoped<FeedRunProgress>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private IRenderedComponent<IComponent> RenderPage() =>
		Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedsPage>(1);
			builder.CloseComponent();
		});

	private static List<IElement> Buttons(IRenderedComponent<IComponent> cut, string label) =>
		[.. cut.FindAll("button").Where(button => button.TextContent.Contains(label))];

	private static FeedOverview Overview() => Overview(Comedy, "Comedy");

	private static TraceSnapshot Snapshot(Optional<FeedRunEnd> ended)
	{
		var fates = FateTally.None with { Started = 3, Matched = 2 };
		return new TraceSnapshot(new TraceCursor(Some.Of(Guid.NewGuid()), 4), 900, [], [], fates, new None(), ended);
	}

	private static FeedOverview Overview(FeedId id, string name)
	{
		return new FeedOverview(
			new Feed
			{
				Name = name,
				Id = id,
				Enabled = true,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "hdlists/comedy",
				CreatedAt = DateTimeOffset.UtcNow,
				Schedule = new FeedRunTrigger(new Manual()),
			},
			new None(),
			new FeedRunHistory
			{
				TotalRuns = 0,
				FirstRunAt = new None(),
				TotalAdded = 0,
				TotalRemoved = 0,
			},
			0
		);
	}
}
