using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.Media;
using DB.Data.Watchlist;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class WatchlistRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private WatchlistRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new WatchlistRepository(fixture.ContextFactory);

	[Test]
	public async Task AddAsync_BatchOfDistinctMedia_CreatesSeparateEntries()
	{
		var first = await SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);

		await _testee.AddAsync([first, second], new WatchlistItemProvenance(new RemoteOrigin()));

		var entries = await fixture.Context.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(2);
	}

	[Test]
	public async Task AddAsync_EmptyCollection_DoesNothing()
	{
		await _testee.AddAsync([], new WatchlistItemProvenance(new ManualOrigin()));

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

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaEntity();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private async Task<MediaId> SeedWatchlistEntryAsync(IExternalMediaId id, WatchlistEntryOrigin origin)
	{
		var media = new MediaEntity();
		await fixture.Context.ExternalMediaIds.AddAsync(id.ToExternalIdEntity(media));
		fixture.Context.Watchlist.Add(new WatchlistEntity { Media = media, Origin = origin });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}
}
