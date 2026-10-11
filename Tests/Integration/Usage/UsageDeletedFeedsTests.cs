using Contracts.Feed;
using Contracts.Usage;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Usage;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UsageDeletedFeedsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 10, 0, TimeSpan.Zero);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task DeleteAsync_AFeed_KeepsItsNameForTheUsagePage()
	{
		host.SetCurrentTime(Now);
		var feed = await SeedFeedAsync();

		await host.Resolve<IFeedService>().DeleteAsync(feed);

		await Assert.That(await host.Resolve<IDeletedFeeds>().ReadAsync()).IsEquivalentTo([new DeletedFeed(feed, "Horror movies", FeedProvider.Mdblist, Now)]);
		await Assert.That((await host.Resolve<IFeedService>().GetAsync()).Feeds).IsEmpty();
	}

	[Test]
	public async Task PruneAsync_PastTheDeletion_ForgetsTheDeletedFeed()
	{
		host.SetCurrentTime(Now);
		await host.Resolve<IFeedService>().DeleteAsync(await SeedFeedAsync());
		var store = host.Resolve<IUsageStore>();

		await store.PruneAsync(Now.AddMinutes(-1));
		var kept = await host.Resolve<IDeletedFeeds>().ReadAsync();
		await store.PruneAsync(Now.AddMinutes(1));

		await Assert.That(kept).Count().IsEqualTo(1);
		await Assert.That(await host.Resolve<IDeletedFeeds>().ReadAsync()).IsEmpty();
	}

	private async Task<FeedId> SeedFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = "Horror movies",
			IsEnabled = false,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = "hdlists/horror-movies-2001-2020",
		};
		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		return FeedId.From(entity.Id);
	}
}
