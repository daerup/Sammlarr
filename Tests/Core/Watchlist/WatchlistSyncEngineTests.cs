using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Core.Watchlist.Sync;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace Core.Tests.Watchlist;

public class WatchlistSyncEngineTests
{
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

		await Assert.That(second is WatchlistSyncAlreadyRunning).IsTrue();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult is WatchlistSyncUnchanged).IsTrue();
	}

	[Test]
	public async Task SyncAsync_PushAddOperationCancelled_BreaksWithoutFailingOrCountingMore()
	{
		var firstExternal = PlexId.From("plex-push-cancel-1", MediaType.Movie);
		var secondExternal = PlexId.From("plex-push-cancel-2", MediaType.Movie);
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IExternalMediaId>([]));
		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(ThrowAsync<WatchlistPushResult>(new OperationCanceledException()));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([firstId, secondId]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOperation(firstId, WatchlistOperationKind.Add), new WatchlistOperation(secondId, WatchlistOperationKind.Add)]);

		var mediaRepo = IMediaRepository.Mock();
		mediaRepo
			.GetExternalsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any())
			.Returns(
				(IReadOnlyDictionary<MediaId, IExternalMediaId>)new Dictionary<MediaId, IExternalMediaId> { [firstId] = firstExternal, [secondId] = secondExternal }
			);

		var engine = BuildEngine(provider, repository, outbox, mediaRepo);

		var result = await engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncUnchanged).IsTrue();
		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
	}

	private static WatchlistSyncEngine BuildEngine(
		Mock<IWatchlistProvider> provider,
		Mock<IWatchlistRepository>? repository = null,
		Mock<IWatchlistOutbox>? outbox = null,
		Mock<IMediaRepository>? mediaRepo = null,
		Mock<IMediaService>? media = null
	)
	{
		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
		var services = new ServiceCollection();
		services.AddSingleton((repository ?? EmptyRepository()).Object);
		services.AddSingleton((outbox ?? EmptyOutbox()).Object);
		services.AddSingleton(ProviderConfigStubs.PlexWatchlist().Object);
		services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(index);
		services.AddSingleton((mediaRepo ?? IMediaRepository.Mock()).Object);
		services.AddSingleton((media ?? UnresolvedMedia()).Object);
		var sp = services.BuildServiceProvider();
		var options = new WatchlistSyncOptions { DrainBudget = TimeSpan.FromSeconds(10) };
		return new WatchlistSyncEngine(sp.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<WatchlistSyncEngine>.Instance);
	}

	private static Mock<IWatchlistRepository> EmptyRepository()
	{
		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
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

	private static async IAsyncEnumerable<T> ThrowAsync<T>(Exception ex)
	{
		await Task.CompletedTask;
		throw ex;
#pragma warning disable CS0162
		yield break;
#pragma warning restore CS0162
	}

	private static async IAsyncEnumerable<IExternalMediaId> WaitingAsyncEnumerable(TaskCompletionSource gate)
	{
		await gate.Task;
		yield break;
	}
}
