using Contracts;
using Contracts.Identity;
using Contracts.Watchlist;
using Core.Watchlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Watchlist.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Watchlist;

public class WatchlistSyncEngineTests
{
	private static readonly ICurrentUser User = new TestCurrentUser();
	private static readonly ICurrentUser Other = new TestCurrentUser { Id = UserId.From(new Guid("22222222-2222-2222-2222-222222222222")) };

	[Test]
	public async Task SyncAsync_OverlappingCalls_ReturnsAlreadyRunning()
	{
		var held = new TaskCompletionSource();
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WaitingRemote(held));
		var engine = BuildEngine(provider);

		var first = engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var second = await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(second).IsOfUnionType<WatchlistSyncResult, WatchlistSyncAlreadyRunning>();

		held.SetResult();
		await Assert.That(await first).IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>();
	}

	[Test]
	public async Task SyncAsync_WhileAnotherUserIsSyncing_RunsAnyway()
	{
		var held = new TaskCompletionSource();
		var busy = IWatchlistProvider.Mock();
		busy.GetWatchlistAsync(Any()).Returns(WaitingRemote(held));
		var idle = IWatchlistProvider.Mock();
		idle.GetWatchlistAsync(Any()).Returns(WatchlistStubs.NothingRemote());
		var shared = new WatchlistSyncGate();

		var blocking = BuildEngine(busy, shared).SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var other = await BuildEngine(idle, shared, Other).SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(other).IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>();

		held.SetResult();
		await blocking;
	}

	private static WatchlistSyncEngine BuildEngine(Mock<IWatchlistProvider> provider, WatchlistSyncGate? gate = null, ICurrentUser? user = null)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UnixEpoch);
		var outbox = WatchlistStubs.EmptyOutbox();

		return new WatchlistSyncEngine(
			gate ?? new WatchlistSyncGate(),
			WatchlistStubs.Planner(provider, outbox),
			ProviderConfigStubs.PlexWatchlist().Object,
			WatchlistStubs.EmptyRepository().Object,
			outbox.Object,
			new WatchlistStubs.NoopPublisher(),
			clock.Object,
			user ?? User,
			NullLogger<WatchlistSyncEngine>.Instance
		);
	}

	private static async IAsyncEnumerable<UnmatchedMedia> WaitingRemote(TaskCompletionSource gate)
	{
		await gate.Task;
		yield break;
	}
}
