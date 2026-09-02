using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Core.Watchlist.Sync;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Watchlist;

public class WatchlistSyncEngineTests
{
	private static readonly ICurrentUser User = new TestCurrentUser();

	[Test]
	public async Task SyncAsync_OverlappingCalls_ReturnsAlreadyRunning()
	{
		var gate = new TaskCompletionSource();
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WaitingAsyncEnumerable(gate));
		var engine = BuildEngine(provider);

		var first = engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), User);
		await Task.Delay(50);
		var second = await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), User);

		await Assert.That(second is WatchlistSyncAlreadyRunning).IsTrue();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult is WatchlistSyncSummary).IsTrue();
	}

	[Test]
	public async Task SyncAsync_PendingAddNotOnRemote_ResetsOutboxAndPublishesPushTrigger()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IExternalMediaId>([]));

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0)]);

		var publisher = new RecordingPublisher();

		var engine = BuildEngine(provider, outbox: outbox, publisher: publisher);

		var result = await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), User);

		await Assert.That(result is WatchlistSyncSummary).IsTrue();
		outbox.EnqueueAsync(WatchlistOperationAction.Add, Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		var triggers = publisher.Published.OfType<WatchlistPushRequest>().ToList();
		await Assert.That(triggers.Count).IsEqualTo(1);
		await Assert.That(triggers[0].MediaId).IsEqualTo(mediaId);
	}

	[Test]
	public async Task SyncAsync_PullInsert_PublishesWatchlistSyncedEvent()
	{
		var pulledId = MediaId.From(Guid.NewGuid());

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IExternalMediaId>([PlexId.From("plex-pull", MediaType.Movie)]));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).Returns([pulledId]);
		repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).Returns([]);

		var publisher = new RecordingPublisher();
		var engine = BuildEngine(provider, repository: repository, media: ResolvedMedia(pulledId), publisher: publisher);

		await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), User);

		var synced = publisher.Published.OfType<WatchlistSyncedEvent>().ToList();
		await Assert.That(synced.Count).IsEqualTo(1);
		await Assert.That(synced[0].PulledAdds).Contains(pulledId);
	}

	[Test]
	public async Task SyncAsync_NoChanges_PublishesNothing()
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IExternalMediaId>([]));

		var publisher = new RecordingPublisher();
		var engine = BuildEngine(provider, publisher: publisher);

		await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), User);

		await Assert.That(publisher.Published.OfType<WatchlistSyncedEvent>()).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_PushBacklogOnly_DoesNotPublishWatchlistSynced()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IExternalMediaId>([]));

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0)]);

		var publisher = new RecordingPublisher();
		var engine = BuildEngine(provider, outbox: outbox, publisher: publisher);

		await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), User);

		await Assert.That(publisher.Published.OfType<WatchlistSyncedEvent>()).IsEmpty();
	}

	[Test]
	public async Task DryRunAsync_RunsInScopeOfGivenUser()
	{
		var user = new TestCurrentUser();
		var userScope = IUserScope.Mock();
		userScope.As(Any<ICurrentUser>()).Returns(new NoopScope());
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IExternalMediaId>([]));
		var engine = BuildEngine(provider, userScope: userScope);

		await engine.DryRunAsync(user);

		userScope.As(user).WasCalled(Times.Once);
	}

	private static WatchlistSyncEngine BuildEngine(
		Mock<IWatchlistProvider> provider,
		Mock<IWatchlistRepository>? repository = null,
		Mock<IWatchlistOutbox>? outbox = null,
		Mock<IMediaService>? media = null,
		Mock<IUserScope>? userScope = null,
		IMessagePublisher? publisher = null
	)
	{
		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
		var services = new ServiceCollection();
		services.AddSingleton((repository ?? EmptyRepository()).Object);
		services.AddSingleton((outbox ?? EmptyOutbox()).Object);
		services.AddSingleton(ProviderConfigStubs.PlexWatchlist().Object);
		services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(index);
		services.AddSingleton((media ?? UnresolvedMedia()).Object);
		services.AddSingleton((userScope ?? NoopUserScope()).Object);
		services.AddSingleton<IMessagePublisher>(publisher ?? new RecordingPublisher());
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UnixEpoch);
		services.AddSingleton<IDateTimeProvider>(clock.Object);
		var sp = services.BuildServiceProvider();
		return new WatchlistSyncEngine(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<WatchlistSyncEngine>.Instance);
	}

	private sealed class RecordingPublisher : IMessagePublisher
	{
		public List<object> Published { get; } = [];

		public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
			where TMessage : notnull
		{
			Published.Add(message);
			return Task.CompletedTask;
		}
	}

	private static Mock<IUserScope> NoopUserScope()
	{
		var userScope = IUserScope.Mock();
		userScope.As(Any<ICurrentUser>()).Returns(new NoopScope());
		return userScope;
	}

	private sealed class NoopScope : IDisposable
	{
		public void Dispose() { }
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

	private static Mock<IMediaService> ResolvedMedia(MediaId id)
	{
		var media = IMediaService.Mock();
		media
			.IdentifyAsync(Any<IEnumerable<ResolveRequest>>(), Any())
			.Returns(
				(requests, _) =>
				{
					var results = new List<Result<MediaIdentity>>();
					foreach (var request in requests)
					{
						results.Add(Success.Of(new MediaIdentity(id, [])));
					}
					return AsAsyncEnumerable(results);
				}
			);
		return media;
	}

	private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(IReadOnlyList<T> items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}

	private static async IAsyncEnumerable<IExternalMediaId> WaitingAsyncEnumerable(TaskCompletionSource gate)
	{
		await gate.Task;
		yield break;
	}
}
