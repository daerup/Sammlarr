using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using DB.Data.Wantlist;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SyncEngineTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WantlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Unknown));
	private static readonly WantlistItemProvenance Remote = new(new SinkOrigin(SinkKind.Plex));

	private static readonly WantlistSyncTrigger Trigger = new(new ManualTrigger());

	private IWantlistSyncEngine Testee => host.Resolve<IWantlistSyncEngine>();
	private IWantlistSyncPlanner Planner => host.Resolve<IWantlistSyncPlanner>();
	private IWantlistOutbox Outbox => host.Resolve<IWantlistOutbox>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SyncAsync_WhenAnItemIsRemoteOnly_PullsItIntoTheLocalWantlist()
	{
		await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistHas(TestMedia.Inception);

		var result = await Testee.SyncAsync(Trigger);

		await AssertWantlist(1, WantlistItemOrigin.Sink);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWantlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Adds.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenItPullsChanges_AnnouncesWhatItAddedAndRemoved()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		var fightClub = await host.Movie(TestMedia.FightClub).OnWantlist(origin: Manual);
		await host.RemoteWantlistHas(TestMedia.Inception);
		var heard = host.Heard();

		await Testee.SyncAsync(Trigger);

		var changed = heard.OfType<WantlistChangedEvent>().Single();
		await Assert.That(changed.Added).IsEquivalentTo([inception]);
		await Assert.That(changed.Removed).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task SyncAsync_WhenALocalItemIsMissingRemotely_HardDeletesIt()
	{
		await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await host.RemoteWantlistEmpty();

		var result = await Testee.SyncAsync(Trigger);

		await AssertWantlistEmpty();
		await Assert.That(host.RemoteWantlistPlexIds).IsEmpty();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Removes.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenAnItemIsOnBothSides_LeavesItUntouched()
	{
		await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await host.RemoteWantlistHas(TestMedia.Inception);

		var result = await Testee.SyncAsync(Trigger);

		await AssertWantlist(1, WantlistItemOrigin.Manual);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWantlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.TotalChanges).IsEqualTo(0));
	}

	[Test]
	public async Task SyncAsync_WhenAnAddIsPending_PushesItAndClearsTheOutbox()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistEmpty();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWantlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderAlreadyReflectsAPendingAdd_ConfirmsWithoutRepushing()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistHas(TestMedia.Inception);
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertWantlist(1, WantlistItemOrigin.Sink);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWantlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Adds.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderAlreadyReflectsAPendingRemove_ConfirmsAndDropsTheEntry()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);
		await host.RemoteWantlistEmpty();

		await Testee.SyncAsync(Trigger);

		await AssertWantlistEmpty();
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWantlistPlexIds).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenAPendingRemoveIsStillRemote_PushesItButKeepsItUntilConfirmed()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);
		await host.RemoteWantlistHas(TestMedia.Inception);
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);

		await Testee.SyncAsync(Trigger);

		await AssertOutboxEmpty();
		await AssertWantlist(1, WantlistItemOrigin.Sink);
		await Assert.That(host.RemoteWantlistContains(TestMedia.Inception)).IsFalse();
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderRejectsAPush_KeepsItInTheOutbox()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistEmpty();
		await host.ProviderRejectsPushes();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertOutbox(WantlistOperationAction.Add, inception, attempts: 1);
		await Assert.That(host.RemoteWantlistPlexIds).IsEmpty();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderRejectsARemove_KeepsItQueuedAndRemote()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);
		await host.RemoteWantlistHas(TestMedia.Inception);
		await host.ProviderRejectsPushes();
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);

		var result = await Testee.SyncAsync(Trigger);

		await AssertOutbox(WantlistOperationAction.Remove, inception, attempts: 1);
		await AssertWantlist(1, WantlistItemOrigin.Sink);
		await Assert.That(host.RemoteWantlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task SyncAsync_WhenAPendingAddHasNoPushableId_KeepsItInTheOutbox()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").SeedAsync();
		await host.RemoteWantlistEmpty();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [solaris]);

		await Testee.SyncAsync(Trigger);

		await AssertOutbox(WantlistOperationAction.Add, solaris, attempts: 1);
		await Assert.That(host.RemoteWantlistPlexIds).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenTheRemoteFetchFails_AbortsWithoutMutatingLocalState()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);
		await host.RemoteWantlistUnavailable();

		var result = await Testee.SyncAsync(Trigger);

		await Assert.That(result).IsOfUnionType<WantlistSyncResult, WantlistSyncSinkUnreachable>();
		await AssertWantlist(1, WantlistItemOrigin.Manual);
		await AssertOutbox(WantlistOperationAction.Remove, inception);
	}

	[Test]
	public async Task PlanAsync_PreviewsChangesWithoutApplyingThem()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.RemoteWantlistHas(TestMedia.FightClub);
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		var result = await Planner.PlanAsync();

		await Assert
			.That(result)
			.IsOfUnionType<WantlistPlanResult, WantlistSyncSummary>()
			.And.Multiple(async summary =>
			{
				await Assert.That(summary.TotalChanges).IsEqualTo(2);
				await AssertPlanned(summary, WantlistSyncDirection.Pull, WantlistOperationAction.Add, fightClub);
				await AssertPlanned(summary, WantlistSyncDirection.Push, WantlistOperationAction.Add, inception);
			});
		await AssertWantlist(1, WantlistItemOrigin.Manual);
		await AssertOutbox(WantlistOperationAction.Add, inception);
	}

	[Test]
	public async Task PlanAsync_WhenTheRemoteIsUnavailable_ReportsAnErrorAndLeavesStateUntouched()
	{
		await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await host.RemoteWantlistUnavailable();

		var result = await Planner.PlanAsync();

		await Assert.That(result).IsOfUnionType<WantlistPlanResult, WantlistSyncSinkUnreachable>();
		await AssertWantlist(1, WantlistItemOrigin.Manual);
	}

	[Test]
	public async Task SyncAsync_WhenItPulls_PublishesAWantlistSyncCompletedNotification()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistHas(TestMedia.Inception);

		await Testee.SyncAsync(Trigger);

		var events = host.Published<WantlistSyncCompletedNotification>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].PulledAdds).Contains(inception);
	}

	[Test]
	public async Task SyncAsync_WhenItOnlyPushes_PublishesASyncedEventReportingThePushedItem()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistEmpty();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		await Testee.SyncAsync(Trigger);

		var events = host.Published<WantlistSyncCompletedNotification>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].PushedAdds).Contains(inception);
	}

	[Test]
	public async Task SyncAsync_WhenNothingChanged_PublishesNoEvents()
	{
		await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWantlistEmpty();

		await Testee.SyncAsync(Trigger);

		await Assert.That(host.Published<WantlistSyncCompletedNotification>()).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenTheRemoteIsUnavailable_PublishesAWantlistSyncFailedNotification()
	{
		await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await host.RemoteWantlistUnavailable();

		await Testee.SyncAsync(Trigger);

		var events = host.Published<WantlistSyncFailedNotification>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].Reason).IsNotEmpty();
		await Assert.That(host.Published<WantlistSyncCompletedNotification>()).IsEmpty();
	}

	[Test]
	public async Task PlanAsync_WhenTheRemoteIsUnavailable_PublishesNothingBecauseNothingWasAttempted()
	{
		await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);
		await host.RemoteWantlistUnavailable();

		await Planner.PlanAsync();

		await Assert.That(host.Published<WantlistSyncFailedNotification>()).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhenAPendingEntryHasAlreadyFailedAttempts_DoesNotResetItsAttemptCount()
	{
		var unpushable = await host.SeedMediaAsync(TmdbId.From("55555", MediaType.Movie));
		await host.RemoteWantlistEmpty();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [unpushable]);
		await Outbox.RecordFailedAttemptAsync([unpushable], WantlistOutboxState.Pending, new None());
		await Outbox.RecordFailedAttemptAsync([unpushable], WantlistOutboxState.Pending, new None());

		await Testee.SyncAsync(Trigger);

		await Assert.That((await Outbox.GetAllAsync()).Single().Attempts).IsEqualTo(3);
	}

	private static async Task AssertPlanned(WantlistSyncSummary summary, WantlistSyncDirection direction, WantlistOperationAction action, MediaId mediaId) =>
		await Assert.That(summary.Changes.Any(change => change.MediaId == mediaId && change.Direction == direction && change.Action == action)).IsTrue();

	private async Task AssertWantlist(int count, WantlistItemOrigin origin)
	{
		var entries = await host.ScopedDbContext.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(count);
		await Assert.That(entries[0].Origin).IsEqualTo(origin);
	}

	private async Task AssertWantlistEmpty() => await Assert.That(await host.ScopedDbContext.Wantlist.CountAsync()).IsEqualTo(0);

	private async Task AssertOutboxEmpty() => await Assert.That(await Outbox.GetAllAsync()).IsEmpty();

	private async Task AssertOutbox(WantlistOperationAction action, MediaId mediaId, int? attempts = null)
	{
		var entries = await Outbox.GetAllAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries.Any(entry => entry.MediaId == mediaId && entry.Action == action)).IsTrue();
		if (attempts is { } expected)
		{
			host.ScopedDbContext.ChangeTracker.Clear();
			var entry = await host.ScopedDbContext.WantlistOutbox.AsNoTracking().FirstAsync(outbox => outbox.Media.Id == mediaId.Value);
			await Assert.That(entry.Attempts).IsEqualTo(expected);
		}
	}
}
