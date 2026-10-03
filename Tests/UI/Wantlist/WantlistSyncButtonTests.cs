using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Bunit.TestDoubles;
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
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Domain.Wantlist.Sync;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Wantlist.Components;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

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
	public async Task Dialog_WhenDismissedAfterAWantlistToggle_ReloadsThePage()
	{
		var cut = await OpenDialogAsync();

		await ToggleWantlistAsync(cut);
		await cut.Find(".change-foot button").ClickAsync(new());

		await Assert.That(Navigation().History.Count()).IsEqualTo(1);
	}

	[Test]
	public async Task Dialog_WhenDismissedWithoutAWantlistToggle_LeavesThePageAlone()
	{
		var cut = await OpenDialogAsync();

		await cut.Find(".change-foot button").ClickAsync(new());

		await Assert.That(Navigation().History).IsEmpty();
	}

	[Test]
	public async Task Dialog_WhenSyncedAfterAWantlistToggle_DoesNotReloadOverTheSync()
	{
		var cut = await OpenDialogAsync();

		await ToggleWantlistAsync(cut);
		await cut.FindAll(".change-foot button")[1].ClickAsync(new());

		await Assert.That(Navigation().History).IsEmpty();
	}

	[Test]
	public async Task Plan_WhenAnAttemptFailsTransiently_RetriesInsteadOfReportingAnError()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).ReturnsSequentially(new WantlistSyncSinkUnreachable("temporary"), Summary(), Summary());

		var cut = Render<Harness>();

		cut.WaitForAssertion(() => planner.PlanAsync(Any()).WasCalled(Times.Exactly(2)), TimeSpan.FromSeconds(10));
		await Assert.That(cut.FindAll(".change-band")).IsEmpty();
	}

	[Test]
	public async Task Plan_WhenEveryAttemptFails_GivesUpAfterThreeTries()
	{
		var (_, planner) = RegisterServices();
		planner.PlanAsync(Any()).Returns(new WantlistSyncSinkUnreachable("unreachable"));

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
		await Assert.That(cut.Find(".sync-status").TagName).IsEqualTo("SPAN");
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
		planner.PlanAsync(Any()).Returns(new WantlistSyncSinkUnreachable("unreachable"));

		var cut = Render<WantlistSyncButton>(parameters => parameters.Add(button => button.ProviderName, "Plex"));

		cut.WaitForState(() => !cut.Find(".sync-status").TextContent.Contains("Comparing"), TimeSpan.FromSeconds(10));
		await Assert.That(cut.Find(".sync-status").TextContent).Contains("Plex unreachable");
	}

	private async Task ToggleWantlistAsync(IRenderedComponent<Harness> cut)
	{
		await cut.Find(".change-row").ClickAsync(new());
		cut.WaitForState(() => WantlistAction(cut) is not null);
		await WantlistAction(cut)!.ClickAsync(new());
	}

	private static IElement? WantlistAction(IRenderedComponent<Harness> cut) =>
		cut.FindAll(".media-detail-sheet-content button")
			.FirstOrDefault(button => button.GetAttribute("aria-label")?.Contains("wantlist", StringComparison.OrdinalIgnoreCase) is true);

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
		new SyncWantlist(new ManualTrigger()),
		JobState.Running,
		DateTimeOffset.UnixEpoch,
		1
	);

	private (Mock<IJobQueue> Queue, Mock<IWantlistSyncPlanner> Planner) RegisterServices(bool runningSync = false)
	{
		Services.AddBlazorBlueprintComponents();
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
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(Detail(Synced))));
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
		Services.AddSingleton<MediaProviderBrandingService>();
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

	private static IReadOnlyList<MediaId> Ids(int count) => [.. Enumerable.Range(0, count).Select(_ => MediaId.From(Guid.NewGuid()))];

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	private static IdentifiedMedia Detail(MediaId id)
	{
		return new IdentifiedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Futurama",
				Tagline = string.Empty,
				Summary = string.Empty,
				PosterUrl = string.Empty,
				BackdropUrl = string.Empty,
				ReleaseDate = new DateOnly(1999, 3, 28),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(22),
				Rating = RatingSummary.None,
			}
		);
	}
}
