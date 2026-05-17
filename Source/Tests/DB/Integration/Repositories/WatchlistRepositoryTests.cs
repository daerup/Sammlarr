using Contracts.Auth;
using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.Feed;
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
	public async Task AddAsync_NewEntry_Persists()
	{
		var id = PlexId.From("plex-100", MediaType.Movie);

		await _testee.AddAsync([id], new WatchlistItemProvenance(new ManualOrigin()));

		var entry = await fixture.Context.WatchlistEntries.AsNoTracking().SingleAsync();
		await Assert.That(entry.Origin).IsEqualTo(WatchlistEntryOrigin.Manual);
	}

	[Test]
	public async Task AddAsync_MissingMediaGroup_BootstrapsGroup()
	{
		var id = PlexId.From("plex-101", MediaType.Movie);

		await _testee.AddAsync([id], new WatchlistItemProvenance(new ManualOrigin()));

		var anyEntry = await fixture.Context.MediaIdEntries.AsNoTracking().AnyAsync(e => e.Identifier == MediaIdentifier.From(id));
		await Assert.That(anyEntry).IsTrue();
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
	public async Task GetIdsAsync_ReturnsAllEntriesForProvider()
	{
		var id = PlexId.From("plex-105", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Manual);

		var ids = await _testee.GetIdsAsync(MediaProvider.Plex);

		await Assert.That(ids).Count().IsEqualTo(1);
		await Assert.That(ids[0]).IsEqualTo<IMediaId>(id);
	}

	[Test]
	public async Task GetEntriesAsync_ReturnsEntries_WithProvenance()
	{
		var id = PlexId.From("plex-106", MediaType.Movie);
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Manual);

		var entries = await _testee.GetEntriesAsync(MediaProvider.Plex);

		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Id).IsEqualTo<IMediaId>(id);
		await Assert.That(entries[0].Provenance.Value).IsTypeOf<ManualOrigin>();
	}

	[Test]
	public async Task FeedDeleted_OrphansAttachedEntries()
	{
		var id = PlexId.From("plex-107", MediaType.Movie);
		var feed = await SeedFeedAsync("My Feed");
		await SeedWatchlistEntryAsync(id, WatchlistEntryOrigin.Feed, sourceFeedId: feed);

		var feedRepo = new FeedRepository(fixture.ContextFactory);
		await feedRepo.DeleteFeedAsync(feed);

		var entry = await fixture.Context.WatchlistEntries.AsNoTracking().SingleAsync();
		await Assert.That(entry.Origin).IsEqualTo(WatchlistEntryOrigin.Orphaned);
		await Assert.That(entry.SourceFeedNameSnapshot).IsEqualTo("My Feed");
		await Assert.That(entry.SourceFeedId).IsNull();
	}

	private async Task<Guid> SeedFeedAsync(string name)
	{
		var feed = new FeedEntity
		{
			Name = name,
			IsEnabled = true,
			Provider = Contracts.Media.Feed.FeedProvider.Mdblist,
			Strategy = Contracts.Media.Feed.FeedAddStrategy.Append,
			Identifier = "x",
		};
		fixture.Context.Feeds.Add(feed);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return feed.Id;
	}

	private async Task<Guid> SeedWatchlistEntryAsync(IMediaId id, WatchlistEntryOrigin origin, Guid? sourceFeedId = null, string? sourceFeedNameSnapshot = null)
	{
		await _groupRepo.UpsertAsync([id]);
		var groupId = await fixture
			.Context.MediaIdEntries.Where(e => e.Identifier == MediaIdentifier.From(id))
			.Select(e => e.MediaIdGroupEntity.Id)
			.SingleAsync();
		var mediaGroup = await fixture.Context.MediaIdGroups.FindAsync(groupId);
		var entry = new WatchlistEntryEntity
		{
			Media = mediaGroup!,
			Origin = origin,
			SourceFeedId = sourceFeedId,
			SourceFeedNameSnapshot = sourceFeedNameSnapshot,
		};
		fixture.Context.WatchlistEntries.Add(entry);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return entry.Id;
	}
}
