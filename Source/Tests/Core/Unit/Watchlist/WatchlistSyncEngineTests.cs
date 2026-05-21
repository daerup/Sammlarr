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
		var scenario = Scenario.Build(remote: [remoteId]);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success => await Assert.That(success.Inserted).IsEqualTo(1));
		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_RemoteThrows_ReturnsRemoteFailed_LocalUnchanged()
	{
		var scenario = Scenario.Build(remoteThrows: new InvalidOperationException("network down"));

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncRemoteFailed>()
			.And.Multiple(async failure => await Assert.That(failure.Reason).IsEqualTo("network down"));
		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		scenario.Repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_OverlappingCalls_ReturnsAlreadyRunning()
	{
		var gate = new TaskCompletionSource();
		var scenario = Scenario.Build(remoteEnumerable: WaitingAsyncEnumerable(gate));

		var first = scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var second = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(second).IsOfUnionType<WatchlistSyncResult, WatchlistSyncAlreadyRunning>();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult).IsOfUnionType<WatchlistSyncResult, WatchlistSyncUnchanged>();
	}

	[Test]
	public async Task SyncAsync_ConfirmsOutboxAdd_WhenPresentOnRemote()
	{
		IMediaId id = PlexId.From("plex-1", MediaType.Movie);
		var scenario = Scenario.Build(remote: [id], local: [id], outbox: [new WatchlistOperation(id, WatchlistOperationKind.Add)]);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success =>
			{
				await Assert.That(success.Confirmed).IsEqualTo(1);
				await Assert.That(success.PushedAdds).IsEqualTo(0);
			});
		scenario.Outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_HardDeletesEntry_WhenAbsentFromRemote()
	{
		var id = PlexId.From("plex-deleted-remote", MediaType.Movie);
		var scenario = Scenario.Build(local: [id]);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success => await Assert.That(success.HardDeleted).IsEqualTo(1));
		scenario.Repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushAdd_DropsOutboxRow_OnSuccess()
	{
		IMediaId id = PlexId.From("plex-push-add", MediaType.Movie);
		var scenario = Scenario.Build(local: [id], outbox: [new WatchlistOperation(id, WatchlistOperationKind.Add)], remoteAddSucceeds: true);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success => await Assert.That(success.PushedAdds).IsEqualTo(1));
		scenario.Provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushRemove_DropsOutboxRow_OnSuccess()
	{
		IMediaId id = PlexId.From("plex-push-remove", MediaType.Movie);
		var scenario = Scenario.Build(remote: [id], outbox: [new WatchlistOperation(id, WatchlistOperationKind.Remove)], remoteRemoveSucceeds: true);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success => await Assert.That(success.PushedRemoves).IsEqualTo(1));
		scenario.Provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.DropAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_NoChangesAnywhere_ReturnsUnchanged()
	{
		var scenario = Scenario.Build();

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result).IsOfUnionType<WatchlistSyncResult, WatchlistSyncUnchanged>();
		scenario.Provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
		scenario.Provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_PushAddThrows_CountsRemoteFailure_OutboxNotDropped()
	{
		IMediaId id = PlexId.From("plex-push-fail", MediaType.Movie);
		var scenario = Scenario.Build(
			local: [id],
			outbox: [new WatchlistOperation(id, WatchlistOperationKind.Add)],
			remoteAddThrows: new InvalidOperationException("provider rejected")
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success =>
			{
				await Assert.That(success.PushedAdds).IsEqualTo(0);
				await Assert.That(success.RemoteFailures).IsEqualTo(1);
			});
		scenario.Outbox.DropAsync(WatchlistOperationKind.Add, Arg.Is<IEnumerable<IMediaId>>(ids => ids.Contains(id)), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_PushAddReturnsFalse_StillDropsOutbox_AndCountsAsPushed()
	{
		IMediaId id = PlexId.From("plex-push-false", MediaType.Movie);
		var scenario = Scenario.Build(local: [id], outbox: [new WatchlistOperation(id, WatchlistOperationKind.Add)], remoteAddSucceeds: false);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert
			.That(result)
			.IsOfUnionType<WatchlistSyncResult, WatchlistSyncSuccess>()
			.And.Multiple(async success =>
			{
				await Assert.That(success.PushedAdds).IsEqualTo(1);
				await Assert.That(success.RemoteFailures).IsEqualTo(0);
			});
		scenario.Outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushAddOperationCancelled_BreaksWithoutFailingOrCountingMore()
	{
		IMediaId first = PlexId.From("plex-push-cancel-1", MediaType.Movie);
		IMediaId second = PlexId.From("plex-push-cancel-2", MediaType.Movie);
		var scenario = Scenario.Build(
			local: [first, second],
			outbox: [new WatchlistOperation(first, WatchlistOperationKind.Add), new WatchlistOperation(second, WatchlistOperationKind.Add)],
			remoteAddThrows: new OperationCanceledException()
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result).IsOfUnionType<WatchlistSyncResult, WatchlistSyncUnchanged>();
		scenario.Provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task DryRunAsync_ReturnsReport_NoWrites()
	{
		IMediaId remoteOnly = PlexId.From("plex-remote-only", MediaType.Movie);
		IMediaId localOnly = PlexId.From("plex-local-only", MediaType.Movie);
		IMediaId synced = PlexId.From("plex-synced", MediaType.Movie);
		var scenario = Scenario.Build(remote: [remoteOnly, synced], local: [localOnly, synced]);

		var dryRun = await scenario.Engine.DryRunAsync();

		await Assert
			.That(dryRun)
			.IsOfUnionType<WatchlistSyncDryRun, WatchlistSyncDryRunReport>()
			.And.Multiple(async report =>
			{
				await Assert.That(report.Changes).Contains(new WatchlistSyncChange(remoteOnly, WatchlistSyncDirection.Pull, WatchlistOperationKind.Add));
				await Assert.That(report.Changes).Contains(new WatchlistSyncChange(localOnly, WatchlistSyncDirection.Pull, WatchlistOperationKind.Remove));
				await Assert.That(report.AddCount).IsEqualTo(1);
				await Assert.That(report.RemoveCount).IsEqualTo(1);
				await Assert.That(report.InSync).IsEqualTo(1);
			});

		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		scenario.Repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		scenario.Provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
		scenario.Provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task DryRunAsync_RemoteThrows_ReturnsRemoteFailed()
	{
		var scenario = Scenario.Build(remoteThrows: new InvalidOperationException("network down"));

		var dryRun = await scenario.Engine.DryRunAsync();

		await Assert
			.That(dryRun)
			.IsOfUnionType<WatchlistSyncDryRun, WatchlistSyncDryRunRemoteFailed>()
			.And.Multiple(async failure => await Assert.That(failure.Reason).IsEqualTo("network down"));
	}

	private sealed record Scenario(
		Mock<IWatchlistProvider> Provider,
		Mock<IWatchlistRepository> Repository,
		Mock<IWatchlistOutbox> Outbox,
		WatchlistSyncEngine Engine
	)
	{
		public static Scenario Build(
			IReadOnlyList<IMediaId>? remote = null,
			IReadOnlyList<IMediaId>? local = null,
			IReadOnlyList<WatchlistOperation>? outbox = null,
			Exception? remoteThrows = null,
			IAsyncEnumerable<IMediaId>? remoteEnumerable = null,
			bool remoteAddSucceeds = false,
			bool remoteRemoveSucceeds = false,
			Exception? remoteAddThrows = null,
			Exception? remoteRemoveThrows = null
		)
		{
			var provider = IWatchlistProvider.Mock();
			provider
				.GetWatchlistAsync(Any())
				.Returns(remoteEnumerable ?? (remoteThrows is not null ? ThrowAsync<IMediaId>(remoteThrows) : AsAsyncEnumerable(remote ?? [])));
			if (remoteAddThrows is not null)
			{
				provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).Throws(remoteAddThrows);
			}
			else
			{
				provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).Returns(remoteAddSucceeds);
			}
			if (remoteRemoveThrows is not null)
			{
				provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).Throws(remoteRemoveThrows);
			}
			else
			{
				provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).Returns(remoteRemoveSucceeds);
			}

			var repository = IWatchlistRepository.Mock();
			repository.GetIdsAsync(Any(), Any()).Returns(local ?? []);

			var outboxMock = IWatchlistOutbox.Mock();
			outboxMock.GetAsync(Any(), Any()).Returns(outbox ?? []);

			var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider);
			var services = new ServiceCollection();
			services.AddSingleton<IWatchlistRepository>(repository);
			services.AddSingleton<IWatchlistOutbox>(outboxMock);
			services.AddSingleton(ProviderConfigStubs.PlexWatchlist().Object);
			services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(index);
			var sp = services.BuildServiceProvider();
			var options = new WatchlistSyncOptions { DrainBudget = TimeSpan.FromSeconds(10) };
			var engine = new WatchlistSyncEngine(sp.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<WatchlistSyncEngine>.Instance);

			return new Scenario(provider, repository, outboxMock, engine);
		}
	}

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

	private static async IAsyncEnumerable<IMediaId> WaitingAsyncEnumerable(TaskCompletionSource gate)
	{
		await gate.Task;
		yield break;
	}
}
