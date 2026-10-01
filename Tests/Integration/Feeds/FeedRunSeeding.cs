using Contracts.Feed;
using Domain.Feed;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

internal static class FeedRunSeeding
{
	public static async Task<WantlistItemProvenance> SeedFeedOriginAsync(this SammlarrTestHost host, FeedId feedId)
	{
		var feed = (await Assert.That(await host.Resolve<IFeedRepository>().FindAsync(feedId)).IsSome())!;
		var run = TestFeeds.Run(feed, Guid.NewGuid(), Conclusiveness.NoItems);
		await host.Resolve<IFeedRepository>().StartRunAsync(run);

		return new FeedOrigin(feed, run);
	}

	public static async Task<Domain.Feed.Feed> FeedAsync(this SammlarrTestHost host, FeedId feedId) =>
		(await Assert.That((await host.Resolve<IFeedService>().GetAsync()).Find(feedId)).IsSome())!.Feed;
}
