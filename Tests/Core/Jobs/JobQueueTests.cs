using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Jobs;
using Domain.Feed;
using Domain.Jobs;
using TestInfrastructure;

namespace Core.Tests.Jobs;

public class JobQueueTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task EnqueueAsync_StampsTheRequestWithTheCurrentUserAndTime()
	{
		var feedId = FeedId.From(Guid.NewGuid());
		var (testee, jobs) = BuildTestee();

		await testee.EnqueueAsync(new RunFeed(feedId));

		jobs.EnqueueAsync(new QueuedJob(JobKind.FeedRun, feedId.Value.ToString(), TestCurrentUser.DefaultId, Now), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task EnqueueAsync_OfAWatchlistSync_UsesTheEmptyKeySoTheOwnerAloneCoalescesIt()
	{
		var (testee, jobs) = BuildTestee();

		await testee.EnqueueAsync(new SyncWatchlist());

		jobs.EnqueueAsync(new QueuedJob(JobKind.WatchlistSync, string.Empty, TestCurrentUser.DefaultId, Now), Any()).WasCalled(Times.Once);
	}

	private static (JobQueue Testee, Mock<IJobRepository> Jobs) BuildTestee()
	{
		var jobs = IJobRepository.Mock();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid())));
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);

		return (new JobQueue(jobs.Object, new TestCurrentUser(), clock.Object), jobs);
	}
}
