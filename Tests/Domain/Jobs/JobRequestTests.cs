using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;

namespace Domain.Tests.Jobs;

public class JobRequestTests
{
	[Test]
	public async Task Kind_DiscriminatesTheRequest()
	{
		await Assert.That(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()).Kind).IsEqualTo(JobKind.FeedRun);
		await Assert.That(new SyncWatchlist(new ManualTrigger()).Kind).IsEqualTo(JobKind.WatchlistSync);
	}

	[Test]
	public async Task Key_OfAFeedRun_IsItsFeedIdSoTwoRunsOfOneFeedCoalesce()
	{
		var feedId = FeedId.From(Guid.NewGuid());

		var scheduled = new RunFeed(feedId, new Scheduled("0 0 * * * *"));
		var manual = new RunFeed(feedId, new Manual());

		await Assert.That(scheduled.Key).IsEqualTo(feedId.Value.ToString());
		await Assert.That(manual.Key).IsEqualTo(scheduled.Key);
	}

	[Test]
	public async Task ArmsSync_ForAScheduledRunOrOneAskedToSyncAfter_ButNotAPlainManualRun()
	{
		var feedId = FeedId.From(Guid.NewGuid());

		await Assert.That(new RunFeed(feedId, new Scheduled("0 0 * * * *")).ArmsSync).IsTrue();
		await Assert.That(new RunFeed(feedId, new Manual(), SyncAfter: true).ArmsSync).IsTrue();
		await Assert.That(new RunFeed(feedId, new Manual()).ArmsSync).IsFalse();
	}

	[Test]
	public async Task Key_OfAWatchlistSync_IsEmptyBecauseTheOwnerAloneIdentifiesIt()
	{
		await Assert.That(new SyncWatchlist(new ManualTrigger()).Key).IsEmpty();
	}
}
