using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Harness;

namespace Integration.Tests.Watchlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class OutboxBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WatchlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Unknown));
	private static readonly WatchlistItemProvenance Remote = new(new RemoteOrigin(WatchlistProvider.Plex));

	private IWatchlistService Watchlist => host.Resolve<IWatchlistService>();
	private IWatchlistOutbox Outbox => host.Resolve<IWatchlistOutbox>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Adding_a_new_item_enqueues_an_add_operation()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await Watchlist.AddToWatchlistAsync([inception], Manual);

		await AssertOutbox((WatchlistOperationAction.Add, inception));
	}

	[Test]
	public async Task Adding_an_item_with_a_pending_remove_cancels_the_remove()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		await Watchlist.AddToWatchlistAsync([inception], Manual);

		await AssertOutboxEmpty();
	}

	[Test]
	public async Task Adding_an_empty_collection_leaves_the_outbox_untouched()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Watchlist.AddToWatchlistAsync([], Manual);

		await AssertOutbox((WatchlistOperationAction.Add, inception));
	}

	[Test]
	public async Task Adding_an_already_synced_item_does_not_enqueue_an_add()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);

		await Watchlist.AddToWatchlistAsync([inception], Manual);

		await AssertOutboxEmpty();
	}

	[Test]
	public async Task Adding_an_item_that_already_has_a_pending_add_does_not_duplicate_it()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Watchlist.AddToWatchlistAsync([inception], Manual);

		await AssertOutbox((WatchlistOperationAction.Add, inception));
	}

	[Test]
	public async Task Removing_a_synced_item_enqueues_a_remove_operation()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);

		await Watchlist.RemoveFromWatchlistAsync([inception]);

		await AssertOutbox((WatchlistOperationAction.Remove, inception));
	}

	[Test]
	public async Task Removing_an_item_with_a_pending_add_cancels_the_add()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Watchlist.RemoveFromWatchlistAsync([inception]);

		await AssertOutboxEmpty();
	}

	[Test]
	public async Task Removing_an_item_that_already_has_a_pending_remove_does_not_duplicate_it()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		await Watchlist.RemoveFromWatchlistAsync([inception]);

		await AssertOutbox((WatchlistOperationAction.Remove, inception));
	}

	[Test]
	public async Task Removing_an_empty_collection_leaves_the_outbox_untouched()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await Outbox.EnqueueAsync(WatchlistOperationAction.Add, [inception]);

		await Watchlist.RemoveFromWatchlistAsync([]);

		await AssertOutbox((WatchlistOperationAction.Add, inception));
	}

	[Test]
	public async Task Adding_the_same_item_twice_in_one_batch_enqueues_a_single_add()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await Watchlist.AddToWatchlistAsync([inception, inception], Manual);

		await AssertOutbox((WatchlistOperationAction.Add, inception));
	}

	[Test]
	public async Task Mixed_batch_partitions_uncancels_pushes_and_no_ops_correctly()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		var matrix = await host.Movie("The Matrix", tmdb: "603", plex: "603").OnWatchlist(origin: Remote);
		await Outbox.EnqueueAsync(WatchlistOperationAction.Remove, [inception]);

		await Watchlist.AddToWatchlistAsync([inception, fightClub, matrix], Manual);

		await AssertOutbox((WatchlistOperationAction.Add, fightClub));
	}

	private async Task AssertOutbox(params (WatchlistOperationAction Action, MediaId MediaId)[] expected)
	{
		var entries = await Outbox.GetAsync();
		await Assert.That(entries).Count().IsEqualTo(expected.Length);
		foreach (var (action, mediaId) in expected)
		{
			await Assert.That(entries.Any(entry => entry.Action == action && entry.MediaId == mediaId)).IsTrue();
		}
	}

	private async Task AssertOutboxEmpty() => await Assert.That(await Outbox.GetAsync()).IsEmpty();
}
