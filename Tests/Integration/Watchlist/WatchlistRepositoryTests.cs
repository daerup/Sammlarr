using DB.Data.Media;
using DB.Data.Watchlist;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Watchlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WatchlistRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private WatchlistRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new WatchlistRepository(fixture.ContextFactory);

	[Test]
	public async Task AddAsync_BatchOfDistinctMedia_CreatesSeparateEntries()
	{
		var first = await SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);

		await _testee.AddAsync([first, second], new WatchlistItemProvenance(new RemoteOrigin(WatchlistProvider.Plex)));

		var entries = await fixture.Context.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(2);
	}

	[Test]
	public async Task AddAsync_ReturnsOnlyNewlyInsertedIds()
	{
		var alreadyWatchlisted = await SeedWatchlistEntryAsync(PlexId.From("plex-300", MediaType.Movie), WatchlistEntryOrigin.Manual);
		var newMedia = await SeedMediaAsync([PlexId.From("plex-301", MediaType.Movie)]);
		var unknown = MediaId.From(Guid.NewGuid());

		var added = await _testee.AddAsync([alreadyWatchlisted, newMedia, unknown], new WatchlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

		await Assert.That(added).Count().IsEqualTo(1);
		await Assert.That(added).Contains(newMedia);
	}

	[Test]
	public async Task RemoveAsync_ReturnsOnlyActuallyRemovedIds()
	{
		var watchlisted = await SeedWatchlistEntryAsync(PlexId.From("plex-302", MediaType.Movie), WatchlistEntryOrigin.Manual);
		var unknown = MediaId.From(Guid.NewGuid());

		var removed = await _testee.RemoveAsync([watchlisted, unknown]);

		await Assert.That(removed).Count().IsEqualTo(1);
		await Assert.That(removed).Contains(watchlisted);
	}

	[Test]
	public async Task AddAsync_EmptyCollection_DoesNothing()
	{
		await _testee.AddAsync([], new WatchlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

		var count = await fixture.Context.Watchlist.CountAsync();
		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task RemoveAsync_EmptyCollection_DoesNothing()
	{
		await SeedWatchlistEntryAsync(PlexId.From("plex-108", MediaType.Movie), WatchlistEntryOrigin.Manual);

		await _testee.RemoveAsync([]);

		var count = await fixture.Context.Watchlist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RemoveAsync_UnknownId_IsNoOp()
	{
		await SeedWatchlistEntryAsync(PlexId.From("plex-109", MediaType.Movie), WatchlistEntryOrigin.Manual);
		var unknown = MediaId.From(Guid.NewGuid());

		await _testee.RemoveAsync([unknown]);

		var count = await fixture.Context.Watchlist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task GetIdsAsync_ReturnsAllEntries()
	{
		var mediaId = await SeedWatchlistEntryAsync(PlexId.From("plex-105", MediaType.Movie), WatchlistEntryOrigin.Manual);

		var ids = await _testee.GetIdsAsync();

		await Assert.That(ids).Count().IsEqualTo(1);
		await Assert.That(ids[0]).IsEqualTo(mediaId);
	}

	[Test]
	public async Task GetEntriesAsync_ReturnsEntries_WithProvenance()
	{
		var mediaId = await SeedWatchlistEntryAsync(PlexId.From("plex-106", MediaType.Movie), WatchlistEntryOrigin.Manual);

		var entries = await _testee.GetEntriesAsync();

		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].MediaId).IsEqualTo(mediaId);
		await Assert.That(entries[0].Provenance.Value).IsTypeOf<ManualOrigin>();
	}

	[Test]
	public async Task GetEntriesAsync_RemoteEntry_CarriesStoredWatchlistProvider()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-107", MediaType.Movie)]);
		await _testee.AddAsync([mediaId], new WatchlistItemProvenance(new RemoteOrigin(WatchlistProvider.Plex)));

		var entries = await _testee.GetEntriesAsync();

		await Assert.That(entries[0].Provenance).IsOfUnionType<WatchlistItemProvenance, RemoteOrigin>();
		var remote = (RemoteOrigin)entries[0].Provenance.Value!;
		await Assert.That(remote.Provider).IsEqualTo(WatchlistProvider.Plex);
	}

	[Test]
	public async Task GetEntriesAsync_ManualEntry_CarriesStoredSource()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-110", MediaType.Movie)]);
		await _testee.AddAsync([mediaId], new WatchlistItemProvenance(new ManualOrigin(ManualSource.Browse)));

		var entries = await _testee.GetEntriesAsync();

		await Assert.That(entries[0].Provenance).IsOfUnionType<WatchlistItemProvenance, ManualOrigin>();
		var manual = (ManualOrigin)entries[0].Provenance.Value!;
		await Assert.That(manual.Source).IsEqualTo(ManualSource.Browse);
	}

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private async Task<MediaId> SeedWatchlistEntryAsync(IExternalMediaId id, WatchlistEntryOrigin origin)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddAsync(id.ToExternalIdEntity(media));
		fixture.Context.Watchlist.Add(new WatchlistRow { Media = media, Origin = origin });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}
}
