using Contracts.Identity;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using DB.Data.Watchlist;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Watchlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SyncEngineTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WatchlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Unknown));
	private static readonly WatchlistItemProvenance Remote = new(new RemoteOrigin(WatchlistProvider.Plex));

	private static readonly WatchlistSyncTrigger Trigger = new(new ManualTrigger());

	private IWatchlistSyncEngine Testee => host.Resolve<IWatchlistSyncEngine>();
	private IWatchlistSyncPlanner Planner => host.Resolve<IWatchlistSyncPlanner>();
	private IWatchlistOutbox Outbox => host.Resolve<IWatchlistOutbox>();
	private ICurrentUser CurrentUser => host.Resolve<ICurrentUser>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SyncAsync_WhenAnItemIsRemoteOnly_PullsItIntoTheLocalWatchlist()
	{
		await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.Inception);

		var result = await Testee.SyncAsync(Trigger);

		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Adds.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenALocalItemIsMissingRemotely_HardDeletesIt()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistEmpty();

		var result = await Testee.SyncAsync(Trigger);

		await AssertWatchlistEmpty();
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Removes.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenAnItemIsOnBothSides_LeavesItUntouched()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistHas(TestMedia.Inception);

		var result = await Testee.SyncAsync(Trigger);

		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.TotalChanges).IsEqualTo(0));
	}

	[Test]
	public async Task SyncAsync_WhenAnAddIsPending_PushesItAndClearsTheOutbox()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderAlreadyReflectsAPendingAdd_ConfirmsWithoutRepushing()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.Inception);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Adds.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderAlreadyReflectsAPendingRemove_ConfirmsAndDropsTheEntry()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);
		await host.RemoteWatchlistEmpty();

		await Testee.SyncAsync(Trigger);

		await AssertWatchlistEmpty();
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenAPendingRemoveIsStillRemote_PushesItButKeepsItUntilConfirmed()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await host.RemoteWatchlistHas(TestMedia.Inception);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		await Testee.SyncAsync(Trigger);

		await AssertOutboxEmpty();
		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsFalse();
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderRejectsAPush_KeepsItInTheOutbox()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await host.ProviderRejectsPushes();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertOutbox(WatchlistOperationAction.Add, inception, attempts: 1);
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderRejectsARemove_KeepsItQueuedAndRemote()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await host.RemoteWatchlistHas(TestMedia.Inception);
		await host.ProviderRejectsPushes();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertOutbox(WatchlistOperationAction.Remove, inception, attempts: 1);
		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenAPendingAddHasNoPushableId_KeepsItInTheOutbox()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [solaris]);

		await Testee.SyncAsync(Trigger);

		await AssertOutbox(WatchlistOperationAction.Add, solaris, attempts: 1);
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenTheRemoteFetchFails_AbortsWithoutMutatingLocalState()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);
		await host.RemoteWatchlistUnavailable();

		var result = await Testee.SyncAsync(Trigger);

		await Assert.That(result).IsOfUnionType<WatchlistSyncResult, WatchlistSyncRemoteFailed>();
		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
		await AssertOutbox(WatchlistOperationAction.Remove, inception);
	}

	[Test]
	public async Task PlanAsync_PreviewsChangesWithoutApplyingThem()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.FightClub);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Planner.PlanAsync();

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistPlanResult, WatchlistSyncSummary>()
			.And.Multiple(async summary =>
			{
				await Assert.That(summary.TotalChanges).IsEqualTo(2);
				await AssertPlanned(summary, WatchlistSyncDirection.Pull, WatchlistOperationAction.Add, fightClub);
				await AssertPlanned(summary, WatchlistSyncDirection.Push, WatchlistOperationAction.Add, inception);
			});
		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
		await AssertOutbox(WatchlistOperationAction.Add, inception);
	}

	[Test]
	public async Task PlanAsync_WhenTheRemoteIsUnavailable_ReportsAnErrorAndLeavesStateUntouched()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistUnavailable();

		var result = await Planner.PlanAsync();

		await Assert.That(result).IsOfUnionType<WatchlistPlanResult, WatchlistSyncRemoteFailed>();
		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
	}

	[Test]
	public async Task SyncAsync_WhenItPulls_PublishesAWatchlistSyncedEvent()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.Inception);

		await Testee.SyncAsync(Trigger);

		var events = host.Published<WatchlistSyncedEvent>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].PulledAdds).Contains(inception);
	}

	[Test]
	public async Task SyncAsync_WhenItOnlyPushes_PublishesASyncedEventReportingThePushedItem()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Testee.SyncAsync(Trigger);

		var events = host.Published<WatchlistSyncedEvent>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].QueuedAdds).Contains(inception);
	}

	[Test]
	public async Task SyncAsync_WhenNothingChanged_PublishesNoEvents()
	{
		await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();

		await Testee.SyncAsync(Trigger);

		await Assert.That(host.Published<WatchlistSyncedEvent>()).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenTheRemoteIsUnavailable_PublishesAWatchlistSyncFailedEvent()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistUnavailable();

		await Testee.SyncAsync(Trigger);

		var events = host.Published<WatchlistSyncFailedEvent>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].Reason).IsNotEmpty();
		await Assert.That(host.Published<WatchlistSyncedEvent>()).IsEmpty();
	}

	[Test]
	public async Task PlanAsync_WhenTheRemoteIsUnavailable_PublishesNothingBecauseNothingWasAttempted()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistUnavailable();

		await Planner.PlanAsync();

		await Assert.That(host.Published<WatchlistSyncFailedEvent>()).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenAnAddIsPending_RepublishesAPushRequest()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Testee.SyncAsync(Trigger);

		await Assert.That(host.Published<WatchlistPushRequest>().Any(push => push.MediaId == inception)).IsTrue();
	}

	private static async Task AssertPlanned(WatchlistSyncSummary summary, WatchlistSyncDirection direction, WatchlistOperationAction action, MediaId mediaId) =>
		await Assert.That(summary.Changes.Any(change => change.MediaId == mediaId && change.Direction == direction && change.Action == action)).IsTrue();

	private async Task AssertWatchlist(int count, WatchlistEntryOrigin origin)
	{
		var entries = await host.ScopedDbContext.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(count);
		await Assert.That(entries[0].Origin).IsEqualTo(origin);
	}

	private async Task AssertWatchlistEmpty() => await Assert.That(await host.ScopedDbContext.Watchlist.CountAsync()).IsEqualTo(0);

	private async Task AssertOutboxEmpty() => await Assert.That(await Outbox.GetAsync()).IsEmpty();

	private async Task AssertOutbox(WatchlistOperationAction action, MediaId mediaId, int? attempts = null)
	{
		var entries = await Outbox.GetAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries.Any(entry => entry.MediaId == mediaId && entry.Action == action)).IsTrue();
		if (attempts is { } expected)
		{
			host.ScopedDbContext.ChangeTracker.Clear();
			var entry = await host.ScopedDbContext.WatchlistOutbox.AsNoTracking().FirstAsync(outbox => outbox.Media.Id == mediaId.Value);
			await Assert.That(entry.Attempts).IsEqualTo(expected);
		}
	}
}
