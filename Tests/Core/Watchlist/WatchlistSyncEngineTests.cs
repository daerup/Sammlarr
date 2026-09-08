using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Watchlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
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

		await Assert.That(second).IsOfUnionType<WatchlistSyncResult, WatchlistSyncAlreadyRunning>();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult).IsOfUnionType<WatchlistSyncResult, WatchlistSyncSummary>();
	}

	private static WatchlistSyncEngine BuildEngine(Mock<IWatchlistProvider> provider)
	{
		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UnixEpoch);

		var services = new ServiceCollection();
		services.AddSingleton(EmptyRepository().Object);
		services.AddSingleton(EmptyOutbox().Object);
		services.AddSingleton(ProviderConfigStubs.PlexWatchlist().Object);
		services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(index);
		services.AddSingleton(UnresolvedMedia().Object);
		services.AddSingleton(NoopUserScope().Object);
		services.AddSingleton<IMessagePublisher>(new NoopPublisher());
		services.AddSingleton<IDateTimeProvider>(clock.Object);

		var serviceProvider = services.BuildServiceProvider();
		return new WatchlistSyncEngine(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<WatchlistSyncEngine>.Instance);
	}

	private sealed class NoopPublisher : IMessagePublisher
	{
		public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
			where TMessage : notnull => Task.CompletedTask;
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
