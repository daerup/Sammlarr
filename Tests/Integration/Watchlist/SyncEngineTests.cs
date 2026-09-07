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
	private IWatchlistOutbox Outbox => host.Resolve<IWatchlistOutbox>();
	private ICurrentUser CurrentUser => host.Resolve<ICurrentUser>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task A_remote_only_item_is_pulled_into_the_local_watchlist()
	{
		await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.Inception);

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Adds.Count).IsEqualTo(1));
	}

	[Test]
	public async Task A_local_item_missing_from_the_remote_is_hard_deleted()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistEmpty();

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertWatchlistEmpty();
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Removes.Count).IsEqualTo(1));
	}

	[Test]
	public async Task An_item_on_both_the_remote_and_the_local_watchlist_is_left_untouched()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistHas(TestMedia.Inception);

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.TotalChanges).IsEqualTo(0));
	}

	[Test]
	public async Task A_pending_add_is_pushed_to_the_provider_and_cleared_from_the_outbox()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task A_pending_add_the_provider_already_reflects_is_confirmed_without_re_pushing()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.Inception);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Pull.Adds.Count).IsEqualTo(1));
	}

	[Test]
	public async Task A_pending_remove_the_provider_already_reflects_is_confirmed_and_the_entry_is_dropped()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);
		await host.RemoteWatchlistEmpty();

		await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertWatchlistEmpty();
		await AssertOutboxEmpty();
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
	}

	[Test]
	public async Task A_pending_remove_for_an_item_still_on_the_remote_is_pushed_but_kept_locally_until_confirmed()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await host.RemoteWatchlistHas(TestMedia.Inception);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertOutboxEmpty();
		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsFalse();
	}

	[Test]
	public async Task A_push_the_provider_rejects_is_kept_in_the_outbox()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await host.ProviderRejectsPushes();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertOutbox(WatchlistOperationAction.Add, inception, attempts: 1);
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task A_remove_the_provider_rejects_is_kept_in_the_outbox_and_stays_on_the_remote()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await host.RemoteWatchlistHas(TestMedia.Inception);
		await host.ProviderRejectsPushes();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertOutbox(WatchlistOperationAction.Remove, inception, attempts: 1);
		await AssertWatchlist(1, WatchlistEntryOrigin.Remote);
		await Assert.That(host.RemoteWatchlistContains(TestMedia.Inception)).IsTrue();
		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
			.And.Multiple(async summary => await Assert.That(summary.Push.Count).IsEqualTo(1));
	}

	[Test]
	public async Task A_pending_add_for_media_without_a_pushable_id_is_kept_in_the_outbox()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [solaris]);

		await Testee.SyncAsync(Trigger, CurrentUser);

		await AssertOutbox(WatchlistOperationAction.Add, solaris, attempts: 1);
		await Assert.That(host.RemoteWatchlistPlexIds).IsEmpty();
	}

	[Test]
	public async Task A_failed_remote_fetch_aborts_the_sync_without_mutating_local_state()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);
		await host.RemoteWatchlistUnavailable();

		var result = await Testee.SyncAsync(Trigger, CurrentUser);

		await Assert.That(result).IsOfUnionType<WatchlistSyncResult, WatchlistSyncRemoteFailed>();
		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
		await AssertOutbox(WatchlistOperationAction.Remove, inception);
	}

	[Test]
	public async Task A_dry_run_previews_pull_and_push_changes_without_applying_them()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.FightClub);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		var result = await Testee.DryRunAsync(CurrentUser);

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>()
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
	public async Task A_dry_run_reports_an_error_when_the_remote_is_unavailable_and_leaves_state_untouched()
	{
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);
		await host.RemoteWatchlistUnavailable();

		var result = await Testee.DryRunAsync(CurrentUser);

		await Assert.That(result).IsOfUnionType<WatchlistSyncResult, WatchlistSyncRemoteFailed>();
		await AssertWatchlist(1, WatchlistEntryOrigin.Manual);
	}

	[Test]
	public async Task A_pull_publishes_a_watchlist_synced_event()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistHas(TestMedia.Inception);

		await Testee.SyncAsync(Trigger, CurrentUser);

		var events = host.Published<WatchlistSyncedEvent>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].PulledAdds).Contains(inception);
	}

	[Test]
	public async Task A_push_only_sync_publishes_a_synced_event_reporting_the_pushed_item()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Testee.SyncAsync(Trigger, CurrentUser);

		var events = host.Published<WatchlistSyncedEvent>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].PushedAdds).Contains(inception);
	}

	[Test]
	public async Task A_sync_with_no_changes_publishes_no_events()
	{
		await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();

		await Testee.SyncAsync(Trigger, CurrentUser);

		await Assert.That(host.Published<WatchlistSyncedEvent>()).IsEmpty();
	}

	[Test]
	public async Task A_pending_add_republishes_a_push_request_for_the_item()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.RemoteWatchlistEmpty();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Testee.SyncAsync(Trigger, CurrentUser);

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
