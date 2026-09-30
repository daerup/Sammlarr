using Contracts.Feed;
using DB.Data.Feed;
using DB.Data.Media;
using DB.Data.Wantlist;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IFeedRepository _testee = null!;

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
	public async Task DeleteFeedAsync_OrphansAttachedWantlistEntries_PopulatesSnapshot()
	{
		var feedId = await SeedFeedAsync("My Feed", "my-feed", new DateTimeOffset(2026, 07, 01, 12, 0, 0, TimeSpan.Zero));
		var mediaId = PlexId.From("plex-feed-1", MediaType.Movie);
		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.Add(mediaId.ToExternalIdEntity(media));
		fixture.Context.Wantlist.Add(
			new WantlistRow
			{
				Media = media,
				Origin = WantlistItemOrigin.Feed,
				SourceFeedId = feedId.Value,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.DeleteFeedAsync(feedId);

		var entry = await fixture.Context.Wantlist.AsNoTracking().SingleAsync();
		await Assert.That(entry.Origin).IsEqualTo(WantlistItemOrigin.Orphaned);
		await Assert.That(entry.SourceFeedNameSnapshot).IsEqualTo("My Feed");
		await Assert.That(entry.SourceFeedId).IsNull();
	}

	[Test]
	public async Task CreateFeedAsync_PersistsAllFields()
	{
		var createData = new FeedCreateData
		{
			Name = "Top 250",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Mirror(5)),
			Identifier = "top-250",
			Schedule = new FeedRunTrigger(new Scheduled("0 0 9 * * *")),
		};

		await _testee.CreateFeedAsync(createData);

		var feed = (await _testee.GetFeedsAsync()).Single();
		await Assert.That(feed.Name).IsEqualTo("Top 250");
		await Assert.That(feed.Identifier).IsEqualTo("top-250");
		await Assert.That(feed.Provider).IsEqualTo(FeedProvider.Mdblist);
		await Assert.That(feed.Strategy).IsEqualTo(new FeedStrategy(new Mirror(5)));
		await Assert.That(feed.Enabled).IsTrue();
		await Assert.That(feed.Schedule).IsOfUnionType<FeedRunTrigger, Scheduled>();
	}

	[Test]
	public async Task GetFeedByIdAsync_WhenFeedExists_ReturnsProjectedFeed()
	{
		var id = await SeedFeedAsync("Wantlist", "my-wantlist", new DateTimeOffset(2026, 01, 01, 8, 0, 0, TimeSpan.Zero));

		var feed = await _testee.GetFeedByIdAsync(id);

		await Assert.That(feed).IsNotNull();
		await Assert.That(feed!.Name).IsEqualTo("Wantlist");
		await Assert.That(feed.Identifier).IsEqualTo("my-wantlist");
		await Assert.That(feed.Strategy).IsEqualTo(new FeedStrategy(new Append()));
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
			Strategy = new FeedStrategy(new Mirror(5)),
			Identifier = "new-id",
			Schedule = new FeedRunTrigger(new Manual()),
		};

		await _testee.UpdateFeedAsync(id, updateData);

		var feed = await _testee.GetFeedByIdAsync(id);
		await Assert.That(feed!.Name).IsEqualTo("New Name");
		await Assert.That(feed.Identifier).IsEqualTo("new-id");
		await Assert.That(feed.Strategy).IsEqualTo(new FeedStrategy(new Mirror(5)));
		await Assert.That(feed.Enabled).IsFalse();
		await Assert.That(feed.Schedule).IsOfUnionType<FeedRunTrigger, Manual>();
	}

	[Test]
	public async Task UpdateFeedAsync_WhenFeedDoesNotExist_Throws()
	{
		var updateData = new FeedUpdateData
		{
			Name = "Ghost",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = "ghost",
			Schedule = new FeedRunTrigger(new Manual()),
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
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
		};

		fixture.Context.Feeds.Add(entity);
		await fixture.Context.SaveChangesAsync();
		await fixture.Context.Feeds.Where(f => f.Id == entity.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, createdAt));
		fixture.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
