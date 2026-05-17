using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Feed;
using DB.Data.Feed;
using DB.Data.MediaGroup;
using DB.Data.Watchlist;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class FeedRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IFeedRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new FeedRepository(fixture.ContextFactory);

	[Test]
	public async Task GetFeedsAsync_WhenFeedsExist_ReturnsNewestFirst()
	{
		var oldestId = await SeedFeedAsync("Oldest", "oldest", new DateTimeOffset(2026, 01, 01, 8, 0, 0, TimeSpan.Zero));
		var middleId = await SeedFeedAsync("Middle", "middle", new DateTimeOffset(2026, 02, 01, 8, 0, 0, TimeSpan.Zero));
		var newestId = await SeedFeedAsync("Newest", "newest", new DateTimeOffset(2026, 03, 01, 8, 0, 0, TimeSpan.Zero));

		var feeds = await _testee.GetFeedsAsync();

		await Assert.That(feeds).Count().IsEqualTo(3);
		await Assert.That(feeds[0].Id).IsEqualTo(newestId);
		await Assert.That(feeds[1].Id).IsEqualTo(middleId);
		await Assert.That(feeds[2].Id).IsEqualTo(oldestId);
	}

	[Test]
	public async Task GetFeedsAsync_WhenFeedExists_MapsCreatedAt()
	{
		var expectedCreatedAt = new DateTimeOffset(2026, 04, 01, 10, 30, 0, TimeSpan.Zero);
		var id = await SeedFeedAsync("CreatedAt Check", "created-at-check", expectedCreatedAt);

		var feed = (await _testee.GetFeedsAsync()).Single(l => l.Id == id);

		await Assert.That(feed.CreatedAt).IsEqualTo(expectedCreatedAt);
	}

	[Test]
	public async Task DeleteFeedAsync_WhenFeedExists_RemovesFeed()
	{
		var id = await SeedFeedAsync("To Delete", "to-delete", new DateTimeOffset(2026, 05, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteFeedAsync(id);

		var exists = await fixture.Context.Feeds.AnyAsync(l => l.Id == id);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task DeleteFeedAsync_WhenFeedDoesNotExist_DoesNotChangeData()
	{
		var id = await SeedFeedAsync("Keep", "keep", new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteFeedAsync(Guid.NewGuid());

		var feeds = await fixture.Context.Feeds.AsNoTracking().ToListAsync();
		await Assert.That(feeds).Count().IsEqualTo(1);
		await Assert.That(feeds[0].Id).IsEqualTo(id);
	}

	[Test]
	public async Task DeleteFeedAsync_OrphansAttachedWatchlistEntries_PopulatesSnapshot()
	{
		var feedId = await SeedFeedAsync("My Feed", "my-feed", new DateTimeOffset(2026, 07, 01, 12, 0, 0, TimeSpan.Zero));
		var mediaId = PlexId.From("plex-feed-1", MediaType.Movie);
		var group = new MediaIdentifierGroupEntity();
		fixture.Context.MediaIdEntries.Add(new MediaIdentifierGroupEntry { Identifier = MediaIdentifier.From(mediaId), MediaIdGroupEntity = group });
		fixture.Context.WatchlistEntries.Add(
			new WatchlistEntryEntity
			{
				Media = group,
				Origin = WatchlistEntryOrigin.Feed,
				SourceFeedId = feedId,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.DeleteFeedAsync(feedId);

		var entry = await fixture.Context.WatchlistEntries.AsNoTracking().SingleAsync();
		await Assert.That(entry.Origin).IsEqualTo(WatchlistEntryOrigin.Orphaned);
		await Assert.That(entry.SourceFeedNameSnapshot).IsEqualTo("My Feed");
		await Assert.That(entry.SourceFeedId).IsNull();
	}

	private async Task<Guid> SeedFeedAsync(string name, string identifier, DateTimeOffset createdAt)
	{
		var entity = new FeedEntity
		{
			Name = name,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = FeedAddStrategy.Append,
			Identifier = identifier,
		};

		fixture.Context.Feeds.Add(entity);
		await fixture.Context.SaveChangesAsync();
		await fixture.Context.Feeds.Where(f => f.Id == entity.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, createdAt));
		fixture.Context.ChangeTracker.Clear();
		return entity.Id;
	}
}
