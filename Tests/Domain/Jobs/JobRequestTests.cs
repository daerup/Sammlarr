using Domain.Feed;
using Domain.Jobs;

namespace Domain.Tests.Jobs;

public class JobRequestTests
{
	[Test]
	public async Task Kind_DiscriminatesTheRequest()
	{
		await Assert.That(new JobRequest(new RunFeed(FeedId.From(Guid.NewGuid()))).Kind).IsEqualTo(JobKind.FeedRun);
		await Assert.That(new JobRequest(new SyncWatchlist()).Kind).IsEqualTo(JobKind.WatchlistSync);
	}
}
