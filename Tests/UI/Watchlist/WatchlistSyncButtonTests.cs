using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Bunit.TestDoubles;
using Contracts.Announcing;
using Contracts.Blacklist;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Domain.Identity;
using Domain.Jobs;
using Domain.Jobs;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Watchlist.Components;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Watchlist;

public class WatchlistSyncButtonTests : BunitContext
{
	private static readonly MediaId Synced = MediaId.From(new Guid(7, 0, 0, new byte[8]));
	private static readonly UserId Viewer = UserId.From(new Guid(9, 0, 0, new byte[8]));
	private readonly TestEventPublisher events = new();

	[Test]
	public async Task Button_WhileTheViewersSyncJobIsRunning_ShowsTheSpinnerEvenThoughThisTabDidNotStartIt()
	{
		RegisterServices();
		var cut = Render<WatchlistSyncButton>();
		await Assert.That(cut.Markup).DoesNotContain("Syncing...");

		cut.InvokeAsync(() => events.Publish(new JobStateChangedEvent(Viewer, JobKind.WatchlistSync, string.Empty, JobState.Running)));

		await Assert.That(cut.Markup).Contains("Syncing...");
	}

	[Test]
	public async Task Button_WhenTheQueueAlreadyHasASyncRunning_ShowsTheSpinnerOnFirstRender()
	{
		RegisterServices(runningSync: true);

		var cut = Render<WatchlistSyncButton>();

		await Assert.That(cut.Markup).Contains("Syncing...");
	}

	[Test]
	public async Task Button_WhenAnotherUsersSyncRuns_KeepsTheButtonUsable()
	{
		RegisterServices();
		var cut = Render<WatchlistSyncButton>();

		cut.InvokeAsync(() => events.Publish(new JobStateChangedEvent(UserId.From(Guid.NewGuid()), JobKind.WatchlistSync, string.Empty, JobState.Running)));

		await Assert.That(cut.Markup).DoesNotContain("Syncing...");
	}

	[Test]
	public async Task Dialog_WhenDismissedAfterAWatchlistToggle_ReloadsThePage()
	{
		var cut = await OpenDialogAsync();

		await ToggleWatchlistAsync(cut);
		await cut.Find(".change-foot button").ClickAsync(new());

		await Assert.That(Navigation().History.Count()).IsEqualTo(1);
	}

	[Test]
	public async Task Dialog_WhenDismissedWithoutAWatchlistToggle_LeavesThePageAlone()
	{
		var cut = await OpenDialogAsync();

		await cut.Find(".change-foot button").ClickAsync(new());

		await Assert.That(Navigation().History).IsEmpty();
	}

	[Test]
	public async Task Dialog_WhenSyncedAfterAWatchlistToggle_DoesNotReloadOverTheSync()
	{
		var cut = await OpenDialogAsync();

		await ToggleWatchlistAsync(cut);
		await cut.FindAll(".change-foot button")[1].ClickAsync(new());

		await Assert.That(Navigation().History).IsEmpty();
	}

	[Test]
	public async Task Plan_WhenAnAttemptFailsTransiently_RetriesInsteadOfReportingAnError()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).ReturnsSequentially(new WatchlistSyncRemoteFailed("temporary"), Summary(), Summary());

		var cut = Render<Harness>();

		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Exactly(2)), TimeSpan.FromSeconds(10));
		await Assert.That(cut.FindAll(".change-band")).IsEmpty();
	}

	[Test]
	public async Task Plan_WhenEveryAttemptFails_GivesUpAfterThreeTries()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(new WatchlistSyncRemoteFailed("unreachable"));

		var cut = Render<Harness>();

		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Exactly(3)), TimeSpan.FromSeconds(10));
		await Task.Delay(TimeSpan.FromSeconds(1.5));
		planner.PlanAsync(Any()).WasCalled(Times.Exactly(3));
	}

	private async Task ToggleWatchlistAsync(IRenderedComponent<Harness> cut)
	{
		await cut.Find(".change-row").ClickAsync(new());
		cut.WaitForState(() => WatchlistAction(cut) is not null);
		await WatchlistAction(cut)!.ClickAsync(new());
	}

	private static IElement? WatchlistAction(IRenderedComponent<Harness> cut) =>
		cut.FindAll(".media-detail-sheet-content button")
			.FirstOrDefault(button => button.GetAttribute("aria-label")?.Contains("watchlist", StringComparison.OrdinalIgnoreCase) is true);

	private BunitNavigationManager Navigation() => (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

	private async Task<IRenderedComponent<Harness>> OpenDialogAsync()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(Summary());

		var cut = Render<Harness>();
		await cut.Find("button").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-row").Count == 1);
		return cut;
	}

	private static readonly PendingJob RunningSync = new(
		JobId.From(Guid.NewGuid()),
		new SyncWatchlist(new ManualTrigger()),
		JobState.Running,
		DateTimeOffset.UnixEpoch,
		1
	);

	private (Mock<IJobQueue> Queue, Mock<IWatchlistSyncPlanner> Planner) RegisterServices(bool runningSync = false)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		Services.AddSingleton<IEventPublisher>(events);

		var queueView = IJobQueueView.Mock();
		queueView.GetAsync(Any()).Returns(new QueueSnapshot(runningSync ? [RunningSync] : [], 0));
		Services.AddSingleton(queueView.Object);

		var queue = IJobQueue.Mock();
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid())));
		Services.AddSingleton(queue.Object);

		var planner = IWatchlistSyncPlanner.Mock();
		Services.AddSingleton(planner.Object);

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(Detail(Synced))));
		Services.AddSingleton(media.Object);

		var watchlist = IWatchlistService.Mock();
		watchlist.GetWatchlistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		Services.AddSingleton(watchlist.Object);

		var blacklist = IBlacklistService.Mock();
		blacklist.GetBlacklistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		Services.AddSingleton(blacklist.Object);

		var outbox = IWatchlistOutbox.Mock();
		outbox.FindPendingAsync(Any(), Any()).Returns((WatchlistOutboxEntry?)null);
		Services.AddSingleton(outbox.Object);

		var providerConfig = IProviderConfigRepository.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(providerConfig.Object);

		var currentUser = ICurrentUser.Mock();
		currentUser.Id.Getter.Returns(Viewer);
		Services.AddSingleton(currentUser.Object);
		Services.AddScoped<Notifier>();
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
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
			builder.OpenComponent<WatchlistSyncButton>(1);
			builder.CloseComponent();
		}
	}

	private static WatchlistSyncSummary Summary() => new(new WatchlistDelta([], []), new WatchlistDelta([], []), [Synced]);

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	private static IdentifiedMedia Detail(MediaId id) =>
		new PartialMovie
		{
			Identity = new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			Title = "Futurama",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(1999, 3, 28),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(22),
			Rating = RatingSummary.None,
		};
}
