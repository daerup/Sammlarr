using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Watchlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
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
		var gate = new TaskCompletionSource();
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WaitingAsyncEnumerable(gate));
		var engine = BuildEngine(provider);

		var first = engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var second = await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(second).IsOfUnionType<WatchlistSyncResult, WatchlistSyncAlreadyRunning>();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult).IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>();
	}

	[Test]
	public async Task SyncAsync_WhileAnotherUserIsSyncing_RunsAnyway()
	{
		var held = new TaskCompletionSource();
		var busy = IWatchlistProvider.Mock();
		busy.GetWatchlistAsync(Any()).Returns(WaitingAsyncEnumerable(held));
		var idle = IWatchlistProvider.Mock();
		idle.GetWatchlistAsync(Any()).Returns(NothingRemote());
		var shared = new WatchlistSyncGate();

		var blocking = BuildEngine(busy, gate: shared).SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var other = await BuildEngine(idle, gate: shared, user: Other).SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(other).IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>();

		held.SetResult();
		await blocking;
	}

	[Test]
	public async Task DryRunAsync_WhenAQueuedRemovalTheProviderAlreadyAgreesWith_DropsItWithoutRunningASync()
	{
		var stale = MediaId.From(new Guid(11, 0, 0, new byte[8]));
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(stale, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0)]);
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(NothingRemote());

		await BuildEngine(provider, outbox).DryRunAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task DryRunAsync_WhenTheOutboxAgreesWithTheProvider_LeavesItAlone()
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(NothingRemote());
		var outbox = EmptyOutbox();

		await BuildEngine(provider, outbox).DryRunAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}

	private static async IAsyncEnumerable<UnmatchedMedia> NothingRemote()
	{
		await Task.CompletedTask;
		yield break;
	}

	private static WatchlistSyncEngine BuildEngine(
		Mock<IWatchlistProvider> provider,
		Mock<IWatchlistOutbox>? outbox = null,
		WatchlistSyncGate? gate = null,
		ICurrentUser? user = null
	)
	{
		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UnixEpoch);

		return new WatchlistSyncEngine(
			gate ?? new WatchlistSyncGate(),
			ProviderConfigStubs.PlexWatchlist().Object,
			EmptyRepository().Object,
			(outbox ?? EmptyOutbox()).Object,
			UnresolvedMedia().Object,
			index,
			new NoopPublisher(),
			clock.Object,
			user ?? User,
			NullLogger<WatchlistSyncEngine>.Instance
		);
	}

	private sealed class NoopPublisher : IMessagePublisher
	{
		public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
			where TMessage : notnull => Task.CompletedTask;
	}

	private static Mock<IWatchlistRepository> EmptyRepository()
	{
		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).Returns([]);
		repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).Returns([]);
		return repository;
	}

	private static Mock<IWatchlistOutbox> EmptyOutbox()
	{
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([]);
		return outbox;
	}

	private static Mock<IMediaService> UnresolvedMedia()
	{
		var media = IMediaService.Mock();
		media.IdentifyAsync(Any<IEnumerable<ResolveRequest>>(), Any()).Returns((requests, _) => AsAsyncEnumerable(requests.Select(Unresolved).ToList()));
		return media;
	}

	private static Result<MediaIdentity> Unresolved(ResolveRequest request) => Error.From("unresolved");

	private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(IReadOnlyList<T> items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}

	private static async IAsyncEnumerable<UnmatchedMedia> WaitingAsyncEnumerable(TaskCompletionSource gate)
	{
		await gate.Task;
		yield break;
	}
}
