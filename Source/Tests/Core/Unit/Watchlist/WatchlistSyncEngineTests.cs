using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Core.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using Test.Shared;

namespace Core.Tests.Unit.Watchlist;

public class WatchlistSyncEngineTests
{
	[Test]
	public async Task SyncAsync_InsertsRemoteOnly_AsRemoteOrigin()
	{
		IMediaId remoteId = PlexId.From("plex-1", MediaType.Movie);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable(remoteId));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([]);

		var testee = CreateEngine(provider, repository, outbox);

		var result = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result.Value).IsTypeOf<WatchlistSyncSuccess>();
		await Assert.That(((WatchlistSyncSuccess)result.Value!).Inserted).IsEqualTo(1);
		repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_RemoteThrows_ReturnsRemoteFailed_LocalUnchanged()
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(ThrowAsync<IMediaId>(new InvalidOperationException("network down")));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([]);

		var testee = CreateEngine(provider, repository, outbox);

		var result = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result.Value).IsTypeOf<WatchlistSyncRemoteFailed>();
		await Assert.That(((WatchlistSyncRemoteFailed)result.Value!).Reason).IsEqualTo("network down");
		repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_OverlappingCalls_ReturnsAlreadyRunning()
	{
		var gate = new TaskCompletionSource();

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WaitingAsyncEnumerable(gate));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([]);

		var testee = CreateEngine(provider, repository, outbox);

		var first = testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var second = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(second.Value).IsTypeOf<WatchlistSyncAlreadyRunning>();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult.Value).IsTypeOf<WatchlistSyncUnchanged>();
	}

	[Test]
	public async Task SyncAsync_ConfirmsOutboxAdd_WhenPresentOnRemote()
	{
		IMediaId remoteId = PlexId.From("plex-1", MediaType.Movie);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable(remoteId));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([remoteId]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([new WatchlistOperation(remoteId, WatchlistOperationKind.Add)]);

		var testee = CreateEngine(provider, repository, outbox);

		var result = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result.Value).IsTypeOf<WatchlistSyncSuccess>();
		var success = (WatchlistSyncSuccess)result.Value!;
		await Assert.That(success.Confirmed).IsEqualTo(1);
		await Assert.That(success.PushedAdds).IsEqualTo(0);
		outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_HardDeletesEntry_WhenAbsentFromRemote()
	{
		var localOnly = PlexId.From("plex-deleted-remote", MediaType.Movie);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IMediaId>());

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([localOnly]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([]);

		var testee = CreateEngine(provider, repository, outbox);

		var result = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result.Value).IsTypeOf<WatchlistSyncSuccess>();
		await Assert.That(((WatchlistSyncSuccess)result.Value!).HardDeleted).IsEqualTo(1);
		repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushAdd_DropsOutboxRow_OnSuccess()
	{
		IMediaId addId = PlexId.From("plex-push-add", MediaType.Movie);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable<IMediaId>());
		provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).Returns(true);

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([addId]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([new WatchlistOperation(addId, WatchlistOperationKind.Add)]);

		var testee = CreateEngine(provider, repository, outbox);

		var result = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result.Value).IsTypeOf<WatchlistSyncSuccess>();
		await Assert.That(((WatchlistSyncSuccess)result.Value!).PushedAdds).IsEqualTo(1);
		provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasCalled(Times.Once);
		outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushRemove_DropsOutboxRow_OnSuccess()
	{
		IMediaId removeId = PlexId.From("plex-push-remove", MediaType.Movie);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable(removeId));
		provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).Returns(true);

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([new WatchlistOperation(removeId, WatchlistOperationKind.Remove)]);

		var testee = CreateEngine(provider, repository, outbox);

		var result = await testee.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result.Value).IsTypeOf<WatchlistSyncSuccess>();
		await Assert.That(((WatchlistSyncSuccess)result.Value!).PushedRemoves).IsEqualTo(1);
		provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).WasCalled(Times.Once);
		outbox.DropAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task DryRunAsync_ReturnsReady_WithComputedPlan_NoWrites()
	{
		IMediaId remoteOnly = PlexId.From("plex-remote-only", MediaType.Movie);
		IMediaId localOnly = PlexId.From("plex-local-only", MediaType.Movie);
		IMediaId pendingAdd = PlexId.From("plex-pending-add", MediaType.Movie);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(AsAsyncEnumerable(remoteOnly, pendingAdd));

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([localOnly, pendingAdd]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([new WatchlistOperation(pendingAdd, WatchlistOperationKind.Add)]);

		var testee = CreateEngine(provider, repository, outbox);

		var dryRun = await testee.DryRunAsync();

		await Assert.That(dryRun.Value).IsTypeOf<WatchlistSyncDryRunReport>();
		var report = (WatchlistSyncDryRunReport)dryRun.Value!;
		await Assert.That(report.Changes).Contains(new WatchlistSyncChange(remoteOnly, WatchlistSyncDirection.Pull, WatchlistOperationKind.Add));
		await Assert.That(report.Changes).Contains(new WatchlistSyncChange(localOnly, WatchlistSyncDirection.Pull, WatchlistOperationKind.Remove));
		await Assert.That(report.AddCount).IsEqualTo(1);
		await Assert.That(report.RemoveCount).IsEqualTo(1);

		repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
		provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task DryRunAsync_RemoteThrows_ReturnsRemoteFailed()
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(ThrowAsync<IMediaId>(new InvalidOperationException("network down")));

		var repository = IWatchlistRepository.Mock();
		var outbox = IWatchlistOutbox.Mock();

		var testee = CreateEngine(provider, repository, outbox);

		var dryRun = await testee.DryRunAsync();

		await Assert.That(dryRun.Value).IsTypeOf<WatchlistSyncDryRunRemoteFailed>();
		await Assert.That(((WatchlistSyncDryRunRemoteFailed)dryRun.Value!).Reason).IsEqualTo("network down");
	}

	private static WatchlistSyncEngine CreateEngine(Mock<IWatchlistProvider> provider, Mock<IWatchlistRepository> repository, Mock<IWatchlistOutbox> outbox)
	{
		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
		var configRepository = IProviderConfigRepository.Mock();
		configRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
				}
			);
		var services = new ServiceCollection();
		services.AddSingleton(repository.Object);
		services.AddSingleton(outbox.Object);
		services.AddSingleton(configRepository.Object);
		services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(index);
		var sp = services.BuildServiceProvider();
		var options = new WatchlistSyncOptions { DrainBudget = TimeSpan.FromSeconds(10) };
		return new WatchlistSyncEngine(sp.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<WatchlistSyncEngine>.Instance);
	}

	private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(params T[] items)
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

	private static async IAsyncEnumerable<IMediaId> WaitingAsyncEnumerable(TaskCompletionSource gate)
	{
		await gate.Task;
		yield break;
	}
}
