using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.MediaGroup;
using DB.Data.Watchlist;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class WatchlistRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private WatchlistRepository _testee = null!;
	private MediaIdGroupRepository _groupRepo = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_groupRepo = new MediaIdGroupRepository(fixture.ContextFactory);
		_testee = new WatchlistRepository(fixture.ContextFactory, _groupRepo);
	}

	[Test]
	public async Task AddAsync_NewEntry_PersistsWithProvenance()
	{
		var id = PlexId.From("plex-100", MediaType.Movie);

		await _testee.AddAsync([id], new WatchlistItemProvenance(new ManualOrigin()));

		var entry = await fixture.Context.WatchlistEntries.AsNoTracking().SingleAsync();
		await Assert.That(entry.Origin).IsEqualTo(WatchlistEntryOrigin.Manual);
	}

	[Test]
	public async Task AddAsync_BatchOfDistinctMedia_CreatesSeparateEntries()
	{
		var first = PlexId.From("plex-200", MediaType.Movie);
		var second = PlexId.From("plex-201", MediaType.Movie);

		await _testee.AddAsync([first, second], new WatchlistItemProvenance(new RemoteOrigin()));

		var entries = await fixture.Context.WatchlistEntries.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(2);
	}

	[Test]
	public async Task AddAsync_ExistingEntry_IsIdempotent()
	{
		var id = PlexId.From("plex-102", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Remote);

		await _testee.AddAsync([id], new WatchlistItemProvenance(new ManualOrigin()));

		var entries = await fixture.Context.WatchlistEntries.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Origin).IsEqualTo(WatchlistEntryOrigin.Remote);
	}

	[Test]
	public async Task RemoveAsync_HardDeletes()
	{
		var id = PlexId.From("plex-104", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Manual);

		await _testee.RemoveAsync([id]);

		var count = await fixture.Context.WatchlistEntries.CountAsync();
		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task AddAsync_EmptyCollection_DoesNothing()
	{
		await _testee.AddAsync([], new WatchlistItemProvenance(new ManualOrigin()));

		var count = await fixture.Context.WatchlistEntries.CountAsync();
		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task RemoveAsync_EmptyCollection_DoesNothing()
	{
		var id = PlexId.From("plex-108", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Manual);

		await _testee.RemoveAsync([]);

		var count = await fixture.Context.WatchlistEntries.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RemoveAsync_UnknownId_IsNoOp()
	{
		var seeded = PlexId.From("plex-109", MediaType.Movie);
		var unknown = PlexId.From("plex-never-added", MediaType.Movie);
		await SeedWatchlistEntryAsync(seeded, WatchlistEntryOrigin.Manual);

		await _testee.RemoveAsync([unknown]);

		var count = await fixture.Context.WatchlistEntries.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task GetIdsAsync_ReturnsAllEntriesForProvider()
	{
		var id = PlexId.From("plex-105", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Manual);

		var ids = await _testee.GetIdsAsync(MediaProvider.Plex);

		await Assert.That(ids).Count().IsEqualTo(1);
		await Assert.That(ids[0]).IsEqualTo(id);
	}

	[Test]
	public async Task GetEntriesAsync_ReturnsEntries_WithProvenance()
	{
		var id = PlexId.From("plex-106", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Manual);

		var entries = await _testee.GetEntriesAsync(MediaProvider.Plex);

		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Id).IsEqualTo(id);
		await Assert.That(entries[0].Provenance.Value).IsTypeOf<ManualOrigin>();
	}

	private async Task SeedWatchlistEntryAsync(IMediaId id, WatchlistEntryOrigin origin)
	{
		await _groupRepo.UpsertAsync([id]);
		var group = await fixture.Context.MediaIdEntries.Where(e => e.Identifier == MediaIdentifier.From(id)).Select(e => e.MediaIdGroupEntity).SingleAsync();
		fixture.Context.WatchlistEntries.Add(new WatchlistEntryEntity { Media = group, Origin = origin });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
	}
}
