using System.Net;
using API.Plex;
using Contracts.Media;
using Contracts.Watchlist.Sync;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Integration.Tests.Infrastructure.Media.Plex;
using Microsoft.EntityFrameworkCore;
using Reqnroll.Assist;

namespace Integration.Tests.Watchlist;

[Binding]
public class SyncEngineSteps(SammlarrScenarioContext sammlarr, MediaCatalogContext catalog, LocalMediaContext local, RemoteWatchlistState remote)
{
	[Given("the remote watchlist is empty")]
	public void GivenRemoteEmpty() => InstallRemote();

	[Given("the remote watchlist has:")]
	public void GivenRemoteHas(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
		{
			var resolved = MediaVersionResolver.Resolve(catalog[row.Media!], row);
			var plexId = resolved.Ids[MediaProvider.Plex];
			remote.Known[plexId] = resolved;
			remote.PlexIds.Add(plexId);
		}
		InstallRemote();
	}

	[Given("the remote watchlist is unavailable")]
	public void GivenRemoteUnavailable() =>
		sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.WatchlistPath, HttpStatusCode.InternalServerError);

	[Given("the watchlist provider rejects pushes")]
	public void GivenProviderRejectsPushes()
	{
		sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.AddToWatchlistPath, HttpStatusCode.InternalServerError);
		sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.RemoveFromWatchlistPath, HttpStatusCode.InternalServerError);
	}

	[When("the watchlist syncs")]
	public async Task WhenSync() => await sammlarr.Resolve<IWatchlistSyncEngine>().SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

	[Then(@"the outbox records {int} failed attempt\(s\) for {string}")]
	public async Task ThenOutboxAttempts(int attempts, string key)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var mediaId = local.MediaIds[key].Value;
		var entry = await sammlarr.DbContext.WatchlistOutbox.AsNoTracking().FirstAsync(o => o.Media.Id == mediaId);
		await Assert.That(entry.Attempts).IsEqualTo(attempts);
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
			var resolved = MediaVersionResolver.Resolve(entry, new MediaRow());
			if (resolved.Ids.TryGetValue(MediaProvider.Plex, out var plexId))
				remote.Known.TryAdd(plexId, resolved);
		}
		RemoteWatchlistStub.Install(sammlarr.HttpHandler, remote);
	}

	private string PlexId(string key) => MediaVersionResolver.Resolve(catalog[key], new MediaRow()).Ids[MediaProvider.Plex];
}
