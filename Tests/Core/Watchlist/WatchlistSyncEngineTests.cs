using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Core.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using Test.Shared;

namespace Core.Tests.Watchlist;

public class WatchlistSyncEngineTests
{
	[Test]
	public async Task SyncAsync_InsertsRemoteOnly_AsRemoteOrigin()
	{
		var remoteExternal = PlexId.From("plex-1", MediaType.Movie);
		var remoteMediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(remote: [(remoteExternal, remoteMediaId)]);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.Inserted).IsEqualTo(1);
		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_RemoteThrows_ReturnsRemoteFailed_LocalUnchanged()
	{
		var scenario = Scenario.Build(remoteThrows: new InvalidOperationException("network down"));

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncRemoteFailed).IsTrue();
		var failure = (WatchlistSyncRemoteFailed)result.Value;
		await Assert.That(failure.Reason).IsEqualTo("network down");
		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		scenario.Repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_OverlappingCalls_ReturnsAlreadyRunning()
	{
		var gate = new TaskCompletionSource();
		var scenario = Scenario.Build(remoteEnumerable: WaitingAsyncEnumerable(gate));

		var first = scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var second = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(second is WatchlistSyncAlreadyRunning).IsTrue();

		gate.SetResult();
		var firstResult = await first;
		await Assert.That(firstResult is WatchlistSyncUnchanged).IsTrue();
	}

	[Test]
	public async Task SyncAsync_ConfirmsOutboxAdd_WhenPresentOnRemote()
	{
		var external = PlexId.From("plex-1", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(remote: [(external, mediaId)], local: [mediaId], outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)]);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.Confirmed).IsEqualTo(1);
		await Assert.That(success.PushedAdds).IsEqualTo(0);
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_HardDeletesEntry_WhenAbsentFromRemote()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(local: [mediaId]);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.HardDeleted).IsEqualTo(1);
		scenario.Repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushAdd_DropsOutboxRow_OnSuccess()
	{
		var external = PlexId.From("plex-push-add", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			local: [mediaId],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>> { [mediaId] = new List<IExternalMediaId> { external } },
			outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)],
			remoteAddSucceeds: true
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.PushedAdds).IsEqualTo(1);
		scenario.Provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushRemove_DropsOutboxRow_OnSuccess()
	{
		var external = PlexId.From("plex-push-remove", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			remote: [(external, mediaId)],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>> { [mediaId] = new List<IExternalMediaId> { external } },
			outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Remove)],
			remoteRemoveSucceeds: true
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.PushedRemoves).IsEqualTo(1);
		scenario.Provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_NoChangesAnywhere_ReturnsUnchanged()
	{
		var scenario = Scenario.Build();

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncUnchanged).IsTrue();
		scenario.Provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasNeverCalled();
		scenario.Provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_PushAddThrows_CountsRemoteFailure_OutboxNotDropped()
	{
		var external = PlexId.From("plex-push-fail", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			local: [mediaId],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>> { [mediaId] = new List<IExternalMediaId> { external } },
			outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)],
			remoteAddThrows: new InvalidOperationException("provider rejected")
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.PushedAdds).IsEqualTo(0);
		await Assert.That(success.RemoteFailures).IsEqualTo(1);
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Arg.Is<IEnumerable<MediaId>>(ids => ids.Contains(mediaId)), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_PushAddReturnsFalse_DoesNotDropOutbox_CountsAsFailed()
	{
		var external = PlexId.From("plex-push-false", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			local: [mediaId],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>> { [mediaId] = new List<IExternalMediaId> { external } },
			outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)],
			remoteAddSucceeds: false
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncSuccess).IsTrue();
		var success = (WatchlistSyncSuccess)result.Value;
		await Assert.That(success.PushedAdds).IsEqualTo(0);
		await Assert.That(success.RemoteFailures).IsEqualTo(1);
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Arg.Is<IEnumerable<MediaId>>(ids => ids.Contains(mediaId)), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SyncAsync_PushAddReturnsFalse_RecordsOutboxFailure()
	{
		var external = PlexId.From("plex-push-false-record", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			local: [mediaId],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>> { [mediaId] = new List<IExternalMediaId> { external } },
			outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)],
			remoteAddSucceeds: false
		);

		await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		scenario
			.Outbox.RecordFailureAsync(Any<WatchlistOperationKind>(), Arg.Is<IEnumerable<MediaId>>(ids => ids.Contains(mediaId)), Any())
			.WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushAddMissingExternal_RecordsOutboxFailure()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			local: [mediaId],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>>(),
			outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)]
		);

		await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		scenario
			.Outbox.RecordFailureAsync(Any<WatchlistOperationKind>(), Arg.Is<IEnumerable<MediaId>>(ids => ids.Contains(mediaId)), Any())
			.WasCalled(Times.AtLeastOnce);
	}

	[Test]
	public async Task SyncAsync_PushAddOperationCancelled_BreaksWithoutFailingOrCountingMore()
	{
		var firstExternal = PlexId.From("plex-push-cancel-1", MediaType.Movie);
		var secondExternal = PlexId.From("plex-push-cancel-2", MediaType.Movie);
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			local: [firstId, secondId],
			externalsByMediaId: new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>>
			{
				[firstId] = new List<IExternalMediaId> { firstExternal },
				[secondId] = new List<IExternalMediaId> { secondExternal },
			},
			outbox: [new WatchlistOperation(firstId, WatchlistOperationKind.Add), new WatchlistOperation(secondId, WatchlistOperationKind.Add)],
			remoteAddThrows: new OperationCanceledException()
		);

		var result = await scenario.Engine.SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

		await Assert.That(result is WatchlistSyncUnchanged).IsTrue();
		scenario.Provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task DryRunAsync_ReturnsReport_NoWrites()
	{
		var remoteOnlyExternal = PlexId.From("plex-remote-only", MediaType.Movie);
		var localOnlyExternal = PlexId.From("plex-local-only", MediaType.Movie);
		var syncedExternal = PlexId.From("plex-synced", MediaType.Movie);
		var remoteOnlyId = MediaId.From(Guid.NewGuid());
		var localOnlyId = MediaId.From(Guid.NewGuid());
		var syncedId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(remote: [(remoteOnlyExternal, remoteOnlyId), (syncedExternal, syncedId)], local: [localOnlyId, syncedId]);

		var dryRun = await scenario.Engine.DryRunAsync();

		await Assert.That(dryRun is WatchlistSyncDryRun).IsTrue();
		var report = (WatchlistSyncDryRun)dryRun.Value;
		await Assert.That(report.Changes).Contains(new WatchlistSyncChange(remoteOnlyId, WatchlistSyncDirection.Pull, WatchlistOperationKind.Add));
		await Assert.That(report.Changes).Contains(new WatchlistSyncChange(localOnlyId, WatchlistSyncDirection.Pull, WatchlistOperationKind.Remove));
		await Assert.That(report.AddCount).IsEqualTo(1);
		await Assert.That(report.RemoveCount).IsEqualTo(1);
		await Assert.That(report.InSync).IsEqualTo(1);

		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		scenario.Repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
		scenario.Provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasNeverCalled();
		scenario.Provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task DryRunAsync_RemoteThrows_ReturnsRemoteFailed()
	{
		var scenario = Scenario.Build(remoteThrows: new InvalidOperationException("network down"));

		var dryRun = await scenario.Engine.DryRunAsync();

		await Assert.That(dryRun is Error).IsTrue();
		var failure = (Error)dryRun.Value;
		await Assert.That(failure.Reason).IsEqualTo("network down");
	}

	private sealed record Scenario(
		Mock<IWatchlistProvider> Provider,
		Mock<IWatchlistRepository> Repository,
		Mock<IWatchlistOutbox> Outbox,
		Mock<IMediaRepository> MediaRepo,
		FakeMediaService Media,
		WatchlistSyncEngine Engine
	)
	{
		public static Scenario Build(
			IReadOnlyList<(IExternalMediaId External, MediaId Id)>? remote = null,
			IReadOnlyList<MediaId>? local = null,
			IReadOnlyList<WatchlistOperation>? outbox = null,
			IReadOnlyDictionary<MediaId, IReadOnlyList<IExternalMediaId>>? externalsByMediaId = null,
			Exception? remoteThrows = null,
			IAsyncEnumerable<IExternalMediaId>? remoteEnumerable = null,
			bool remoteAddSucceeds = false,
			bool remoteRemoveSucceeds = false,
			Exception? remoteAddThrows = null,
			Exception? remoteRemoveThrows = null
		)
		{
			var provider = IWatchlistProvider.Mock();
			provider
				.GetWatchlistAsync(Any())
				.Returns(
					remoteEnumerable
						?? (remoteThrows is not null ? ThrowAsync<IExternalMediaId>(remoteThrows) : AsAsyncEnumerable((remote ?? []).Select(r => r.External).ToList()))
				);
			var addExternals = ExternalsFor(outbox, externalsByMediaId, WatchlistOperationKind.Add);
			var removeExternals = ExternalsFor(outbox, externalsByMediaId, WatchlistOperationKind.Remove);
			provider
				.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any())
				.Returns(
					remoteAddThrows is not null
						? ThrowAsync<WatchlistPushResult>(remoteAddThrows)
						: AsAsyncEnumerable(addExternals.Select(e => new WatchlistPushResult(e, remoteAddSucceeds)).ToList())
				);
			provider
				.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any())
				.Returns(
					remoteRemoveThrows is not null
						? ThrowAsync<WatchlistPushResult>(remoteRemoveThrows)
						: AsAsyncEnumerable(removeExternals.Select(e => new WatchlistPushResult(e, remoteRemoveSucceeds)).ToList())
				);

			var repository = IWatchlistRepository.Mock();
			repository.GetIdsAsync(Any()).Returns(local ?? []);

			var outboxMock = IWatchlistOutbox.Mock();
			outboxMock.GetAsync(Any()).Returns(outbox ?? []);

			var media = new FakeMediaService();
			foreach (var (external, mediaId) in remote ?? [])
			{
				media.RegisterResolution(external, mediaId);
			}

			var mediaRepo = IMediaRepository.Mock();
			var flatExternalsByMediaId = (externalsByMediaId ?? new Dictionary<MediaId, IReadOnlyList<IExternalMediaId>>())
				.Where(kvp => kvp.Value.Any(e => e.Source == MediaProvider.Plex))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.First(e => e.Source == MediaProvider.Plex));
			mediaRepo
				.GetExternalsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any())
				.Returns((IReadOnlyDictionary<MediaId, IExternalMediaId>)flatExternalsByMediaId);

			var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
			var services = new ServiceCollection();
			services.AddSingleton<IWatchlistRepository>(repository.Object);
			services.AddSingleton<IWatchlistOutbox>(outboxMock.Object);
			services.AddSingleton(ProviderConfigStubs.PlexWatchlist().Object);
			services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(index);
			services.AddSingleton<IMediaRepository>(mediaRepo.Object);
			services.AddSingleton<IMediaService>(media);
			var sp = services.BuildServiceProvider();
			var options = new WatchlistSyncOptions { DrainBudget = TimeSpan.FromSeconds(10) };
			var engine = new WatchlistSyncEngine(sp.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<WatchlistSyncEngine>.Instance);

			return new Scenario(provider, repository, outboxMock, mediaRepo, media, engine);
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

	private static IReadOnlyList<IExternalMediaId> ExternalsFor(
		IReadOnlyList<WatchlistOperation>? outbox,
		IReadOnlyDictionary<MediaId, IReadOnlyList<IExternalMediaId>>? externalsByMediaId,
		WatchlistOperationKind kind
	)
	{
		if (outbox is null || externalsByMediaId is null)
		{
			return [];
		}
		return
		[
			.. from op in outbox
			where op.Kind == kind
			where externalsByMediaId.ContainsKey(op.MediaId)
			from external in externalsByMediaId[op.MediaId]
			select external,
		];
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
