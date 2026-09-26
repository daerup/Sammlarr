using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Shared.Options;
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

		await testee.EnqueueAsync(new RunFeed(feedId, new Manual()));

		jobs.EnqueueAsync(new QueuedJob(new RunFeed(feedId, new Manual()), TestCurrentUser.DefaultId, Now, TimeSpan.Zero), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task EnqueueAsync_CarriesTheRequestThroughUntouched()
	{
		var (testee, jobs) = BuildTestee();

		await testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		jobs.EnqueueAsync(new QueuedJob(new SyncWatchlist(new ScheduledFeedTrigger()), TestCurrentUser.DefaultId, Now + Options.Grace, Options.MaxWait), Any())
			.WasCalled(Times.Once);
	}

	private static SchedulerOptions Options => new();

	private static (JobQueue Testee, Mock<IJobRepository> Jobs) BuildTestee()
	{
		var jobs = IJobRepository.Mock();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid())));
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);

		return (new JobQueue(jobs.Object, new JobSignal(), new TestCurrentUser(), clock.Object, Options), jobs);
	}
}
