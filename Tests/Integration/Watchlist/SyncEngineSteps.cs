using System.Net;
using API.Plex;
using Contracts;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Watchlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist.Sync;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Integration.Tests.Infrastructure.Media.Plex;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Watchlist;

[Binding]
public class SyncEngineSteps(SammlarrScenarioContext sammlarr, MediaCatalogContext catalog, LocalMediaContext local, RemoteWatchlistState remote)
{
	private WatchlistSyncSummary? _dryRun;
	private bool _dryRunFailed;
	private WatchlistSyncResult _syncResult = default!;

	[Given("the remote watchlist is empty")]
	public void GivenRemoteEmpty() => InstallRemote();

	[Given("the remote watchlist has:")]
	public void GivenRemoteHas(DataTable table)
	{
		foreach (var row in table.CreateSet<HandleRow>())
		{
			var resolved = MediaVersionResolver.Build(catalog[row.Media!]);
			var plexId = resolved.Ids[MediaProvider.Plex];
			remote.Known[plexId] = resolved;
			remote.PlexIds.Add(plexId);
		}
		InstallRemote();
	}

	[Given("the remote watchlist is unavailable")]
	public void GivenRemoteUnavailable() => sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.WatchlistPath, HttpStatusCode.InternalServerError);

	[Given("the watchlist provider rejects pushes")]
	public void GivenProviderRejectsPushes()
	{
		sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.AddToWatchlistPath, HttpStatusCode.InternalServerError);
		sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.RemoveFromWatchlistPath, HttpStatusCode.InternalServerError);
	}

	[When("the watchlist syncs")]
	public async Task WhenSync() =>
		_syncResult = await sammlarr.Resolve<IWatchlistSyncEngine>().SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()), sammlarr.Resolve<ICurrentUser>());

	[When("I perform a dry run")]
	public async Task WhenDryRun()
	{
		var result = await sammlarr.Resolve<IWatchlistSyncEngine>().DryRunAsync(sammlarr.Resolve<ICurrentUser>());
		_dryRun = result is WatchlistSyncSummary changes ? changes : null;
		_dryRunFailed = result is WatchlistSyncRemoteFailed;
	}

	[Then("the dry run plans:")]
	public async Task ThenDryRunPlansTable(DataTable table)
	{
		await Assert.That(_dryRun).IsNotNull();
		var expected = table.CreateSet<DryRunRow>().ToList();
		await Assert.That(_dryRun!.TotalChanges).IsEqualTo(expected.Count);
		foreach (var row in expected)
		{
			var mediaId = local.MediaIds[row.Key!];
			var direction = Enum.Parse<WatchlistSyncDirection>(row.Direction!, ignoreCase: true);
			var kind = Enum.Parse<Domain.Watchlist.Outbox.WatchlistOperationAction>(row.Operation!, ignoreCase: true);
			await Assert.That(_dryRun!.Changes.Any(c => c.MediaId == mediaId && c.Direction == direction && c.Action == kind)).IsTrue();
		}
	}

	[Then("the dry run reports an error")]
	public async Task ThenDryRunError() => await Assert.That(_dryRunFailed).IsTrue();

	[Then("the sync result is:")]
	public async Task ThenSyncResultIs(DataTable table)
	{
		if (_syncResult is not WatchlistSyncSummary success)
		{
			await Assert.That(_syncResult is WatchlistSyncSummary).IsTrue();
			return;
		}
		var row = table.CreateSet<SyncResultRow>().Single();
		if (row.Pulled is { } pulled)
			await Assert.That(success.Pull.Adds.Count).IsEqualTo(pulled);
		if (row.Removed is { } removed)
			await Assert.That(success.Pull.Removes.Count).IsEqualTo(removed);
		if (row.Queued is { } queued)
			await Assert.That(success.Push.Count).IsEqualTo(queued);
	}

	[Then("the sync is unchanged")]
	public async Task ThenSyncIsUnchanged() => await Assert.That(_syncResult is WatchlistSyncSummary { TotalChanges: 0 }).IsTrue();

	[Then("the sync reports a remote failure")]
	public async Task ThenSyncReportsRemoteFailure() => await Assert.That(_syncResult is WatchlistSyncRemoteFailed).IsTrue();

	private sealed class SyncResultRow
	{
		public int? Pulled { get; set; }
		public int? Removed { get; set; }
		public int? Queued { get; set; }
	}

	[Then("the remote watchlist contains {string}")]
	public async Task ThenRemoteContains(string key) => await Assert.That(remote.PlexIds.Contains(PlexId(key))).IsTrue();

	[Then("the remote watchlist does not contain {string}")]
	public async Task ThenRemoteDoesNotContain(string key) => await Assert.That(remote.PlexIds.Contains(PlexId(key))).IsFalse();

	[Then("the remote watchlist is empty")]
	public async Task ThenRemoteEmpty() => await Assert.That(remote.PlexIds).IsEmpty();

	private void InstallRemote()
	{
		foreach (var entry in catalog.Entries)
		{
			var resolved = MediaVersionResolver.Build(entry);
			if (resolved.Ids.TryGetValue(MediaProvider.Plex, out var plexId))
				remote.Known.TryAdd(plexId, resolved);
		}
		RemoteWatchlistStub.Install(sammlarr.HttpHandler, remote);
	}

	private string PlexId(string key) => MediaVersionResolver.Build(catalog[key]).Ids[MediaProvider.Plex];
}
