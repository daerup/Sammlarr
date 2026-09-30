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
		var feed = (await host.Resolve<IFeedRepository>().GetFeedByIdAsync(feedId))!;
		var run = TestFeeds.Run(feed, Guid.NewGuid(), Conclusiveness.NoItems);
		await host.Resolve<IFeedRepository>().StartRunAsync(run);

		return new FeedOrigin(feed, run);
	}
}
