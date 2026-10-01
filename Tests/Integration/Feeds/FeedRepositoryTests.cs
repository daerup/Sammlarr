using Contracts.Feed;
using DB.Data.Feed;
using Domain.Feed;
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
	public async Task GetAllAsync_WhenFeedsExist_ReturnsNewestFirst()
	{
		var oldestId = await SeedFeedAsync("Oldest", "oldest", new DateTimeOffset(2026, 01, 01, 8, 0, 0, TimeSpan.Zero));
		var middleId = await SeedFeedAsync("Middle", "middle", new DateTimeOffset(2026, 02, 01, 8, 0, 0, TimeSpan.Zero));
		var newestId = await SeedFeedAsync("Newest", "newest", new DateTimeOffset(2026, 03, 01, 8, 0, 0, TimeSpan.Zero));

		var feeds = await _testee.GetAllAsync();

		await Assert.That(feeds).Count().IsEqualTo(3);
		await Assert.That(feeds[0].Id).IsEqualTo(newestId);
		await Assert.That(feeds[1].Id).IsEqualTo(middleId);
		await Assert.That(feeds[2].Id).IsEqualTo(oldestId);
	}

	[Test]
	public async Task GetAllAsync_WhenFeedExists_MapsCreatedAt()
	{
		var expectedCreatedAt = new DateTimeOffset(2026, 04, 01, 10, 30, 0, TimeSpan.Zero);
		var id = await SeedFeedAsync("CreatedAt Check", "created-at-check", expectedCreatedAt);

		var feed = (await _testee.GetAllAsync()).Single(l => l.Id == id);

		await Assert.That(feed.CreatedAt).IsEqualTo(expectedCreatedAt);
	}

	[Test]
	public async Task DeleteAsync_WhenFeedExists_RemovesFeed()
	{
		var id = await SeedFeedAsync("To Delete", "to-delete", new DateTimeOffset(2026, 05, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteAsync(id);

		var exists = await fixture.Context.Feeds.AnyAsync(l => l.Id == id.Value);
		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task DeleteAsync_WhenFeedDoesNotExist_DoesNotChangeData()
	{
		var id = await SeedFeedAsync("Keep", "keep", new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero));

		await _testee.DeleteAsync(FeedId.From(Guid.NewGuid()));

		var feeds = await fixture.Context.Feeds.AsNoTracking().ToListAsync();
		await Assert.That(feeds).Count().IsEqualTo(1);
		await Assert.That(feeds[0].Id).IsEqualTo(id.Value);
	}

	[Test]
	public async Task CreateAsync_PersistsAllFields()
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

		await _testee.CreateAsync(createData);

		var feed = (await _testee.GetAllAsync()).Single();
		await Assert.That(feed.Name).IsEqualTo("Top 250");
		await Assert.That(feed.Identifier).IsEqualTo("top-250");
		await Assert.That(feed.Provider).IsEqualTo(FeedProvider.Mdblist);
		await Assert.That(feed.Strategy).IsEqualTo(new FeedStrategy(new Mirror(5)));
		await Assert.That(feed.Enabled).IsTrue();
		await Assert.That(feed.Schedule).IsOfUnionType<FeedRunTrigger, Scheduled>();
	}

	[Test]
	public async Task FindAsync_WhenFeedExists_ReturnsProjectedFeed()
	{
		var id = await SeedFeedAsync("Wantlist", "my-wantlist", new DateTimeOffset(2026, 01, 01, 8, 0, 0, TimeSpan.Zero));

		var feed = await Assert.That(await _testee.FindAsync(id)).IsSome();

		await Assert.That(feed!.Name).IsEqualTo("Wantlist");
		await Assert.That(feed.Identifier).IsEqualTo("my-wantlist");
		await Assert.That(feed.Strategy).IsEqualTo(new FeedStrategy(new Append()));
		await Assert.That(feed.Enabled).IsTrue();
	}

	[Test]
	public async Task FindAsync_WhenFeedDoesNotExist_ReturnsNone()
	{
		await Assert.That(await _testee.FindAsync(FeedId.From(Guid.NewGuid()))).IsNone();
	}

	[Test]
	public async Task UpdateAsync_WhenFeedExists_ChangesAllFields()
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

		await _testee.UpdateAsync(id, updateData);

		var feed = await Assert.That(await _testee.FindAsync(id)).IsSome();
		await Assert.That(feed!.Name).IsEqualTo("New Name");
		await Assert.That(feed.Identifier).IsEqualTo("new-id");
		await Assert.That(feed.Strategy).IsEqualTo(new FeedStrategy(new Mirror(5)));
		await Assert.That(feed.Enabled).IsFalse();
		await Assert.That(feed.Schedule).IsOfUnionType<FeedRunTrigger, Manual>();
	}

	[Test]
	public async Task UpdateAsync_WhenFeedDoesNotExist_Throws()
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

		await Assert.That(async () => await _testee.UpdateAsync(FeedId.From(Guid.NewGuid()), updateData)).Throws<InvalidOperationException>();
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
