using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Announcing;
using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Sync;
using Domain.Blocklist;
using Domain.Identity;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Wantlist.Components;
using UI.Shared.Services;
using UI.Tests.Media;

namespace UI.Tests.Wantlist;

public class WantlistSyncButtonTests : BunitContext
{
	private static readonly MediaId Synced = MediaId.From(new Guid(7, 0, 0, new byte[8]));
	private static readonly UserId Viewer = UserId.From(new Guid(9, 0, 0, new byte[8]));
	private readonly TestEventPublisher events = new();

	[Test]
	public async Task Button_WhileTheViewersSyncJobIsRunning_ShowsTheSpinnerEvenThoughThisTabDidNotStartIt()
	{
		RegisterServices();
		var cut = Render<WantlistSyncButton>();
		await Assert.That(cut.Markup).DoesNotContain("Syncing...");

		await cut.InvokeAsync(() => events.Publish(new JobStateChangedEvent(Viewer, JobKind.WantlistSync, string.Empty, JobState.Running)));

		await Assert.That(cut.Markup).Contains("Syncing...");
	}

	[Test]
	public async Task Button_WhenTheQueueAlreadyHasASyncRunning_ShowsTheSpinnerOnFirstRender()
	{
		RegisterServices(runningSync: true);

		var cut = Render<WantlistSyncButton>();

		await Assert.That(cut.Markup).Contains("Syncing...");
	}

	[Test]
	public async Task Button_WhenAnotherUsersSyncRuns_KeepsTheButtonUsable()
	{
		RegisterServices();
		var cut = Render<WantlistSyncButton>();

		await cut.InvokeAsync(() => events.Publish(new JobStateChangedEvent(UserId.From(Guid.NewGuid()), JobKind.WantlistSync, string.Empty, JobState.Running)));

		await Assert.That(cut.Markup).DoesNotContain("Syncing...");
	}

	[Test]
	public async Task DryRun_WhenTheViewersWantlistChanges_IsPlannedAgain()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary());
		var cut = Render<WantlistSyncButton>();
		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Once));

		await cut.InvokeAsync(() => events.Publish(new WantlistChangedEvent(Viewer, [Synced], [])));

		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Exactly(2)));
	}

	[Test]
	public async Task DryRun_WhenAnotherPersonsWantlistChanges_IsLeftAlone()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary());
		var cut = Render<WantlistSyncButton>();
		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Once));

		await cut.InvokeAsync(() => events.Publish(new WantlistChangedEvent(UserId.From(Guid.NewGuid()), [Synced], [])));

		planner.PlanAsync(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Plan_WhenAnAttemptFailsTransiently_RetriesAndSaysInSyncInsteadOfReportingAnError()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).ReturnsSequentially(new WantlistSyncSinkUnreachable("temporary", []), Summary(), Summary());

		var cut = Render<Harness>();

		cut.WaitForState(() => !cut.Find(".sync-status").TextContent.Contains("Comparing"), TimeSpan.FromSeconds(10));
		await Assert.That(cut.Find(".sync-status").TextContent).Contains("In sync");
	}

	[Test]
	public async Task Plan_WhenEveryAttemptFails_GivesUpAfterThreeTries()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(new WantlistSyncSinkUnreachable("unreachable", []));

		var cut = Render<Harness>();

		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Exactly(3)), TimeSpan.FromSeconds(10));
		await Task.Delay(TimeSpan.FromSeconds(1.5));
		planner.PlanAsync(Any()).WasCalled(Times.Exactly(3));
	}

	[Test]
	public async Task Status_WithChangesInBothDirections_ShowsOneSignedAddAndOneSignedRemoveCount()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary(new WantlistDelta(Ids(12), Ids(3)), new WantlistDelta(Ids(2), Ids(1))));

		var cut = Render<WantlistSyncButton>();

		cut.WaitForElement(".sync-status .is-added");
		await Assert.That(cut.FindAll(".sync-status .is-added").Select(count => count.TextContent)).IsEquivalentTo(["+14"]);
		await Assert.That(cut.FindAll(".sync-status .is-removed").Select(count => count.TextContent)).IsEquivalentTo(["−4"]);
	}

	[Test]
	public async Task Status_BesideTheSyncButton_IsStaticTextRatherThanASecondButton()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary(new WantlistDelta(Ids(5), [])));

		var cut = Render<WantlistSyncButton>();

		cut.WaitForElement(".sync-status .is-added");
		await Assert.That(cut.Find("[role=group]").QuerySelectorAll("button").Length).IsEqualTo(1);
	}

	[Test]
	public async Task Status_WithUnmatchedTitles_LeavesThemOutOfTheCounts()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary() with { Unmatched = [new UnmatchedMedia([], new MovieDetails { Title = "Unsung Hero" })] });

		var cut = Render<WantlistSyncButton>();

		cut.WaitForState(() => !cut.Find(".sync-status").TextContent.Contains("Comparing"));
		await Assert.That(cut.Find(".sync-status").TextContent).Contains("In sync");
	}

	[Test]
	public async Task Status_WhenEveryPlanAttemptFails_NamesTheUnreachableProvider()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(new WantlistSyncSinkUnreachable("unreachable", []));

		var cut = Render<WantlistSyncButton>(parameters => parameters.Add(button => button.ProviderName, "Plex"));

		cut.WaitForState(() => !cut.Find(".sync-status").TextContent.Contains("Comparing"), TimeSpan.FromSeconds(10));
		await Assert.That(cut.Find(".sync-status").TextContent).Contains("Plex unreachable");
	}

	[Test]
	public async Task Status_OnRadarrAndSonarr_SplitsTheCountsPerApp()
	{
		var (_, planner) = RegisterServices();
		var movie = MediaId.New();
		var shows = Ids(3);
		planner.PlanAsync(Any()).Returns(Summary(new WantlistDelta([movie, shows[0], shows[1]], []), new WantlistDelta([], [shows[2]])));
		var media = IMediaService.Mock();
		media
			.GetDetailsAsync(Any(), Any(), Any())
			.Returns((ids, _, _) => Async([.. ids.Select(id => (Result<MatchedMedia>)Success.Of(Typed(id, id == movie ? MediaType.Movie : MediaType.Show)))]));
		Services.AddSingleton(media.Object);

		var cut = Render<WantlistSyncButton>(parameters => parameters.Add(button => button.Sink, SinkKind.Arr));

		cut.WaitForElement("[data-app=Sonarr] .is-removed");
		await Assert.That(Spaceless(cut.Find("[data-app=Radarr]").TextContent)).IsEqualTo("+1");
		await Assert.That(Spaceless(cut.Find("[data-app=Sonarr]").TextContent)).IsEqualTo("+2−1");
	}

	[Test]
	public async Task Status_OnRadarrAndSonarrWhenSonarrIsUnreachable_SaysSoBesideRadarr()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary() with { Down = Some.Of(new WantlistSyncSinkUnreachable("Sonarr answered 401 Unauthorized.", [ArrApp.Sonarr])) });

		var cut = Render<WantlistSyncButton>(parameters => parameters.Add(button => button.Sink, SinkKind.Arr));

		cut.WaitForState(
			() => cut.FindAll("[data-app=Sonarr]").Count == 1 && cut.Find("[data-app=Sonarr]").TextContent.Contains("unreachable"),
			TimeSpan.FromSeconds(10)
		);
		await Assert.That(cut.Find("[data-app=Radarr]").TextContent.Trim()).IsEqualTo("in sync");
	}

	[Test]
	public async Task Status_OnRadarrAndSonarrWhenBothAreUnreachable_SaysSoForBoth()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(new WantlistSyncSinkUnreachable("Radarr is down. Sonarr is down.", [ArrApp.Radarr, ArrApp.Sonarr]));

		var cut = Render<WantlistSyncButton>(parameters => parameters.Add(button => button.Sink, SinkKind.Arr));

		cut.WaitForState(
			() => cut.FindAll(".sync-app").Count == 2 && cut.FindAll(".sync-app").All(app => app.TextContent.Contains("unreachable")),
			TimeSpan.FromSeconds(10)
		);
		await Assert.That(cut.FindAll(".sync-app").Select(app => app.GetAttribute("data-app"))).IsEquivalentTo(["Radarr", "Sonarr"]);
	}

	[Test]
	public async Task Dialog_WithTitlesStillOnTheirWay_NamesTheSinkTheyAreWaitingFor()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary());
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WantlistSyncButton>(1);
			builder.AddComponentParameter(2, nameof(WantlistSyncButton.ProviderName), "Radarr + Sonarr");
			builder.AddComponentParameter(
				3,
				nameof(WantlistSyncButton.OutboxEntries),
				(IReadOnlyCollection<WantlistOutboxEntry>)[new WantlistOutboxEntry(Synced, WantlistOperationAction.Add, WantlistOutboxState.Pending, 0)]
			);
			builder.CloseComponent();
		});

		await cut.Find("button").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".change-pin").Count == 1);

		await Assert.That(cut.Markup).Contains("1 waiting to reach Radarr + Sonarr");
	}

	[Test]
	public async Task Dialog_WhileSonarrIsDown_ShowsSonarrsReasonAndKeepsSyncEnabled()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary() with { Down = Some.Of(new WantlistSyncSinkUnreachable("Sonarr answered 401 Unauthorized.", [ArrApp.Sonarr])) });
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WantlistSyncButton>(1);
			builder.AddComponentParameter(2, nameof(WantlistSyncButton.Sink), SinkKind.Arr);
			builder.AddComponentParameter(
				3,
				nameof(WantlistSyncButton.OutboxEntries),
				(IReadOnlyCollection<WantlistOutboxEntry>)[new WantlistOutboxEntry(Synced, WantlistOperationAction.Add, WantlistOutboxState.Pending, 0)]
			);
			builder.CloseComponent();
		});

		await cut.Find("button").ClickAsync(new());

		cut.WaitForState(() => cut.Markup.Contains("Sonarr answered 401 Unauthorized."));
		await Assert.That(SyncButton(cut).HasAttribute("disabled")).IsFalse();
	}

	[Test]
	public async Task Dialog_WhenReopened_ShowsTheLastPlanWhileItPlansAgain()
	{
		var (_, planner) = RegisterServices();
		var added = MediaId.New();
		planner.PlanAsync(Any()).Returns(Summary(push: new WantlistDelta([added], [])));
		Services.AddSingleton(MediaOf(MediaType.Movie));
		var cut = Render<Harness>();
		await Opener(cut).ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-row").Count == 1 && cut.FindAll(".change-head-spin").Count == 0);
		await cut.FindAll(".change-actions button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync(new());
		planner.PlanAsync(Any()).ReturnsAsync(new TaskCompletionSource<WantlistPlanResult>().Task);

		_ = Opener(cut).ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".change-head-spin").Count == 1);
		await Assert.That(cut.FindAll(".change-row")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll(".change-loading")).IsEmpty();
	}

	[Test]
	public async Task Sync_WhilePlanning_IsEnabled()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).ReturnsAsync(new TaskCompletionSource<WantlistPlanResult>().Task);
		var cut = Render<Harness>();

		_ = Opener(cut).ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".change-loading").Count == 1);
		await Assert.That(SyncButton(cut).HasAttribute("disabled")).IsFalse();
	}

	[Test]
	public async Task Dialog_ForTitlesAlreadyInSync_ReadsTheirDetailsOnlyWhenShown()
	{
		var (_, planner) = RegisterServices();
		var added = MediaId.New();
		planner.PlanAsync(Any()).Returns(Summary(push: new WantlistDelta([added], [])));
		var read = new List<MediaId>();
		var media = IMediaService.Mock();
		media
			.GetDetailsAsync(Any(), Any(), Any())
			.Returns(
				(ids, _, _) =>
				{
					var asked = ids.ToList();
					read.AddRange(asked);
					return Async([.. asked.Select(id => (Result<MatchedMedia>)Success.Of(id == Synced ? Detail(id) : Typed(id, MediaType.Movie)))]);
				}
			);
		Services.AddSingleton(media.Object);
		var cut = Render<Harness>();
		await Opener(cut).ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-pin").Count == 1 && cut.FindAll(".change-head-spin").Count == 0);
		await Assert.That(read).Contains(added);
		await Assert.That(read).DoesNotContain(Synced);

		await cut.Find(".change-pin button").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".change-row").Count == 2);
		await Assert.That(read).Contains(Synced);
		await Assert.That(cut.FindAll(".change-group").Last().TextContent).Contains("Already in sync");
	}

	[Test]
	public async Task Dialog_WhenItComparesAgainAfterShowingThem_ReadsOnlyTheTitlesNotReadYet()
	{
		var (_, planner) = RegisterServices();
		var newlySynced = MediaId.New();
		planner.PlanAsync(Any()).Returns(Summary());
		var read = new List<MediaId>();
		var media = IMediaService.Mock();
		media
			.GetDetailsAsync(Any(), Any(), Any())
			.Returns(
				(ids, _, _) =>
				{
					var asked = ids.ToList();
					read.AddRange(asked);
					return Async([.. asked.Select(id => (Result<MatchedMedia>)Success.Of(Typed(id, MediaType.Movie)))]);
				}
			);
		Services.AddSingleton(media.Object);
		var cut = Render<Harness>();
		await Opener(cut).ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-pin").Count == 1 && cut.FindAll(".change-head-spin").Count == 0);
		await cut.Find(".change-pin button").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-row").Count == 1);
		await cut.FindAll(".change-actions button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync(new());
		planner.PlanAsync(Any()).Returns(new WantlistSyncSummary(NoChanges, NoChanges, [Synced, newlySynced]));

		await Opener(cut).ClickAsync(new());

		cut.WaitForState(() => read.Contains(newlySynced) && cut.FindAll(".change-head-spin").Count == 0);
		await Assert.That(read.Count(id => id == Synced)).IsEqualTo(1);
	}

	private static IElement Opener<T>(IRenderedComponent<T> cut)
		where T : IComponent
	{
		return cut.Find("[aria-label='Wantlist sync'] button");
	}

	private static IElement SyncButton<T>(IRenderedComponent<T> cut)
		where T : IComponent
	{
		return cut.FindAll(".change-actions button").Single(button => button.TextContent.Trim() == "Sync");
	}

	private static IMediaService MediaOf(MediaType type)
	{
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any(), Any()).Returns((ids, _, _) => Async([.. ids.Select(id => (Result<MatchedMedia>)Success.Of(Typed(id, type)))]));
		return media.Object;
	}

	private static readonly PendingJob RunningSync = new(
		JobId.From(Guid.NewGuid()),
		new SyncWantlist(new ManualTrigger()),
		JobState.Running,
		DateTimeOffset.UnixEpoch,
		1
	);

	private (Mock<IJobQueue> Queue, Mock<IWantlistSyncPlanner> Planner) RegisterServices(bool runningSync = false)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();

		Services.AddSingleton<IEventPublisher>(events);

		var queueView = IJobQueueView.Mock();
		queueView.GetAsync(Any()).Returns(new QueueSnapshot(runningSync ? [RunningSync] : [], 0));
		Services.AddSingleton(queueView.Object);

		var queue = IJobQueue.Mock();
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Queued));
		Services.AddSingleton(queue.Object);

		var planner = IWantlistSyncPlanner.Mock();
		Services.AddSingleton(planner.Object);

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any(), Any()).Returns(Async<Result<MatchedMedia>>(Success.Of(Detail(Synced))));
		Services.AddSingleton(media.Object);

		var wantlist = IWantlistService.Mock();
		wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		Services.AddSingleton(wantlist.Object);

		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(blocklist.Object);

		var providerConfig = IProviderConfigService.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(providerConfig.Object);

		var currentUser = ICurrentUser.Mock();
		currentUser.Id.Getter.Returns(Viewer);
		Services.AddSingleton(currentUser.Object);
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return (queue, planner);
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WantlistSyncButton>(1);
			builder.CloseComponent();
		}
	}

	private static readonly WantlistDelta NoChanges = new([], []);

	private static WantlistSyncSummary Summary(WantlistDelta? pull = null, WantlistDelta? push = null) => new(pull ?? NoChanges, push ?? NoChanges, [Synced]);

	private static IReadOnlyList<MediaId> Ids(int count) => [.. Enumerable.Range(0, count).Select(_ => MediaId.New())];

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	private static string Spaceless(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

	private static MatchedMedia Typed(MediaId id, MediaType type)
	{
		MediaDetails details = type == MediaType.Movie ? new MovieDetails { Title = "Dune" } : new ShowDetails { Title = "Dune: Prophecy" };
		return new MatchedMedia(new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), type)]), details);
	}

	private static MatchedMedia Detail(MediaId id)
	{
		return new MatchedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Futurama",
				ReleaseDate = new DateOnly(1999, 3, 28),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(22),
				Ratings = MediaRatings.None,
			}
		);
	}
}
