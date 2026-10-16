using Contracts.Feed;
using Contracts.Usage;
using Domain.Providers;
using Harness;
using TUnit.Assertions.Enums;

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
		var feed = await host.SeedFeedAsync(name: "Horror movies");

		await host.Resolve<IFeedService>().DeleteAsync(feed);

		await Assert.That(await host.Resolve<IDeletedFeeds>().ReadAsync()).IsEquivalentTo([new DeletedFeed(feed, "Horror movies", FeedProvider.Mdblist, Now)]);
		await Assert.That((await host.Resolve<IFeedService>().GetAsync()).Feeds).IsEmpty();
	}

	[Test]
	public async Task PruneAsync_PastTheDeletion_ForgetsTheDeletedFeed()
	{
		host.SetCurrentTime(Now);
		await host.Resolve<IFeedService>().DeleteAsync(await host.SeedFeedAsync(name: "Horror movies"));
		var store = host.Resolve<IUsageStore>();

		await store.PruneAsync(Now.AddMinutes(-1));
		var kept = await host.Resolve<IDeletedFeeds>().ReadAsync();
		await store.PruneAsync(Now.AddMinutes(1));

		await Assert.That(kept).Count().IsEqualTo(1);
		await Assert.That(await host.Resolve<IDeletedFeeds>().ReadAsync()).IsEmpty();
	}

	[Test]
	public async Task PruneAsync_PastTheDeletion_ForgetsEveryUsersDeletedFeeds()
	{
		host.SetCurrentTime(Now);
		await host.SignInAs("Ben");
		await host.Resolve<IFeedService>().DeleteAsync(await host.SeedFeedAsync(name: "Ben's anime"));
		await host.SignInAs("Ann");
		await host.Resolve<IFeedService>().DeleteAsync(await host.SeedFeedAsync(name: "Horror movies"));
		var before = await host.Resolve<IUsageStore>().FeedNamesAsync();

		await host.Resolve<IUsageStore>().PruneAsync(Now.AddMinutes(1));

		await Assert.That(before).Count().IsEqualTo(2);
		await Assert.That(await host.Resolve<IUsageStore>().FeedNamesAsync()).IsEmpty();
	}

	[Test]
	public async Task FeedNamesAsync_ForAnotherUsersFeeds_NamesTheLiveOnesFirstThenTheDeleted()
	{
		host.SetCurrentTime(Now);
		await host.SignInAs("Ben");
		var live = await host.SeedFeedAsync(name: "Ben's anime");
		var gone = await host.SeedFeedAsync(name: "Horror movies");
		await host.Resolve<IFeedService>().DeleteAsync(gone);
		await host.SignInAs("Ann");

		var names = await host.Resolve<IUsageStore>().FeedNamesAsync();

		await Assert
			.That(names)
			.IsEquivalentTo(
				[
					new UsageFeedName(live, "Ben's anime", FeedProvider.Mdblist, new None()),
					new UsageFeedName(gone, "Horror movies", FeedProvider.Mdblist, Some.Of(Now)),
				],
				CollectionOrdering.Matching
			);
	}
}
