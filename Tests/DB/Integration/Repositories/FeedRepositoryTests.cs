using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using DB.Data.Feed;
using DB.Data.Media;
using DB.Data.Watchlist;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
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

		var exists = await fixture.Context.Feeds.AnyAsync(l => l.Id == id.Value);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task DeleteFeedAsync_WhenFeedDoesNotExist_DoesNotChangeData()
	{
		var id = await SeedFeedAsync("Keep", "keep", new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteFeedAsync(FeedId.From(Guid.NewGuid()));

		var feeds = await fixture.Context.Feeds.AsNoTracking().ToListAsync();
		await Assert.That(feeds).Count().IsEqualTo(1);
		await Assert.That(feeds[0].Id).IsEqualTo(id.Value);
	}

	[Test]
	public async Task DeleteFeedAsync_OrphansAttachedWatchlistEntries_PopulatesSnapshot()
	{
		var feedId = await SeedFeedAsync("My Feed", "my-feed", new DateTimeOffset(2026, 07, 01, 12, 0, 0, TimeSpan.Zero));
		var mediaId = PlexId.From("plex-feed-1", MediaType.Movie);
		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.Add(mediaId.ToExternalIdEntity(media));
		fixture.Context.Watchlist.Add(
			new WatchlistRow
			{
				Media = media,
				Origin = WatchlistEntryOrigin.Feed,
				SourceFeedId = feedId.Value,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.DeleteFeedAsync(feedId);

		var entry = await fixture.Context.Watchlist.AsNoTracking().SingleAsync();
		await Assert.That(entry.Origin).IsEqualTo(WatchlistEntryOrigin.Orphaned);
		await Assert.That(entry.SourceFeedNameSnapshot).IsEqualTo("My Feed");
		await Assert.That(entry.SourceFeedId).IsNull();
	}

	[Test]
	public async Task CreateFeedAsync_PersistsAllFields()
	{
		var createData = new FeedCreateData
		{
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = FeedAddStrategy.Replace,
			Identifier = "top-250",
			Schedule = SyncMode.From("0 0 9 * * *"),
		};

		await _testee.CreateFeedAsync("Top 250", createData);

		var feed = (await _testee.GetFeedsAsync()).Single();
		await Assert.That(feed.Name).IsEqualTo("Top 250");
		await Assert.That(feed.Identifier).IsEqualTo("top-250");
		await Assert.That(feed.Provider).IsEqualTo(FeedProvider.Mdblist);
		await Assert.That(feed.Strategy).IsEqualTo(FeedAddStrategy.Replace);
		await Assert.That(feed.Enabled).IsTrue();
		await Assert.That(feed.Schedule.ToString()).IsEqualTo("0 0 9 * * *");
	}

	[Test]
	public async Task GetFeedByIdAsync_WhenFeedExists_ReturnsProjectedFeed()
	{
		var id = await SeedFeedAsync("Watchlist", "my-watchlist", new DateTimeOffset(2026, 01, 01, 8, 0, 0, TimeSpan.Zero));

		var feed = await _testee.GetFeedByIdAsync(id);

		await Assert.That(feed).IsNotNull();
		await Assert.That(feed!.Name).IsEqualTo("Watchlist");
		await Assert.That(feed.Identifier).IsEqualTo("my-watchlist");
		await Assert.That(feed.Strategy).IsEqualTo(FeedAddStrategy.Append);
		await Assert.That(feed.Enabled).IsTrue();
	}

	[Test]
	public async Task GetFeedByIdAsync_WhenFeedDoesNotExist_ReturnsNull()
	{
		var feed = await _testee.GetFeedByIdAsync(FeedId.From(Guid.NewGuid()));

		await Assert.That(feed).IsNull();
	}

	[Test]
	public async Task UpdateFeedAsync_WhenFeedExists_ChangesAllFields()
	{
		var id = await SeedFeedAsync("Old Name", "old-id", new DateTimeOffset(2026, 02, 01, 8, 0, 0, TimeSpan.Zero));
		var updateData = new FeedUpdateData
		{
			Name = "New Name",
			IsEnabled = false,
			Provider = FeedProvider.Mdblist,
			Strategy = FeedAddStrategy.Replace,
			Identifier = "new-id",
			Schedule = new SyncMode(new Manual()),
		};

		await _testee.UpdateFeedAsync(id, updateData);

		var feed = await _testee.GetFeedByIdAsync(id);
		await Assert.That(feed!.Name).IsEqualTo("New Name");
		await Assert.That(feed.Identifier).IsEqualTo("new-id");
		await Assert.That(feed.Strategy).IsEqualTo(FeedAddStrategy.Replace);
		await Assert.That(feed.Enabled).IsFalse();
		await Assert.That(feed.Schedule.ToString()).IsEqualTo("manual");
	}

	[Test]
	public async Task UpdateFeedAsync_WhenFeedDoesNotExist_Throws()
	{
		var updateData = new FeedUpdateData
		{
			Name = "Ghost",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = FeedAddStrategy.Append,
			Identifier = "ghost",
			Schedule = new SyncMode(new Manual()),
		};

		await Assert.That(async () => await _testee.UpdateFeedAsync(FeedId.From(Guid.NewGuid()), updateData)).Throws<InvalidOperationException>();
	}

	private async Task<FeedId> SeedFeedAsync(string name, string identifier, DateTimeOffset createdAt)
	{
		var entity = new FeedRow
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
		return FeedId.From(entity.Id);
	}
}
