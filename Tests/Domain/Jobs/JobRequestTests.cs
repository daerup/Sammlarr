using Domain.Feed;
using Domain.Jobs;
using Domain.Wantlist.Sync;

namespace Domain.Tests.Jobs;

public class JobRequestTests
{
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
	public async Task Key_OfAWantlistSync_IsEmptyBecauseTheOwnerAloneIdentifiesIt()
	{
		await Assert.That(new SyncWantlist(new ManualTrigger()).Key).IsEmpty();
	}
}
