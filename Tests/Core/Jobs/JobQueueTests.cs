using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Announcing;
using Core.Jobs;
using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
		var (testee, jobs, _) = BuildTestee();

		await testee.EnqueueAsync(new RunFeed(feedId, new Manual()));

		jobs.EnqueueAsync(new QueuedJob(new RunFeed(feedId, new Manual()), TestCurrentUser.DefaultId, Now, TimeSpan.Zero, SkipWhileRunning: true), Now, Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task EnqueueAsync_CarriesTheRequestThroughUntouched()
	{
		var (testee, jobs, _) = BuildTestee();

		await testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		jobs.EnqueueAsync(new QueuedJob(new SyncWantlist(new ScheduledFeedTrigger()), TestCurrentUser.DefaultId, Now + Options.Grace, Options.MaxWait), Now, Any())
			.WasCalled(Times.Once);
	}

	private static SchedulerOptions Options => new();

	[Test]
	public async Task EnqueueAsync_WhenItRunsNow_AnnouncesItQueued()
	{
		var (testee, _, events) = BuildTestee();
		List<IEvent> seen = [];
		events.Published += seen.Add;

		await testee.EnqueueAsync(new SyncWantlist(new ManualTrigger()));

		var announced = seen.OfType<JobStateChangedEvent>().Single();
		await Assert.That(announced.State).IsEqualTo(JobState.Queued);
		await Assert.That(announced.Kind).IsEqualTo(JobKind.WantlistSync);
	}

	[Test]
	public async Task EnqueueAsync_AnnouncesTheStateTheRowWasActuallyWrittenWith_NotOneItGuessedFromTheDelay()
	{
		var (testee, jobs, events) = BuildTestee();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any<DateTimeOffset>(), Any()).Returns(new AlreadyQueued(JobId.From(Guid.NewGuid()), JobState.Queued));
		List<IEvent> seen = [];
		events.Published += seen.Add;

		await testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		await Assert.That(seen.OfType<JobStateChangedEvent>().Single().State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task EnqueueAsync_WhenAScheduledFeedQueuesTheWaitingSyncBehindIt_AnnouncesTheSyncQueuedToo()
	{
		var (testee, jobs, events) = BuildTestee();
		var nightly = new RunFeed(FeedId.From(Guid.NewGuid()), Nightly);
		jobs.QueuedAsync(Any<UserId>(), JobKind.FeedRun, Any()).Returns([nightly]);
		jobs.QueueWaitingAsync(Any<UserId>(), JobKind.WantlistSync, Any<DateTimeOffset>(), Any()).Returns(1);
		List<IEvent> seen = [];
		events.Published += seen.Add;

		await testee.EnqueueAsync(nightly);

		await Assert
			.That(seen.OfType<JobStateChangedEvent>().Select(announced => (announced.Kind, announced.State)))
			.IsEquivalentTo([(JobKind.FeedRun, JobState.Queued), (JobKind.WantlistSync, JobState.Queued)]);
	}

	[Test]
	public async Task EnqueueAsync_ForAPlainManualRun_LeavesTheWaitingSyncAlone()
	{
		var (testee, jobs, _) = BuildTestee();

		await testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		jobs.QueueWaitingAsync(Any<UserId>(), Any<JobKind>(), Any<DateTimeOffset>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task EnqueueAsync_WhenTheArmedSyncLandsBehindAQueuedScheduledFeed_ReportsItQueuedRatherThanWaiting()
	{
		var (testee, jobs, events) = BuildTestee();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any<DateTimeOffset>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Waiting));
		jobs.QueuedAsync(Any<UserId>(), JobKind.FeedRun, Any()).Returns([new RunFeed(FeedId.From(Guid.NewGuid()), Nightly)]);
		jobs.QueueWaitingAsync(Any<UserId>(), JobKind.WantlistSync, Any<DateTimeOffset>(), Any()).Returns(1);
		List<IEvent> seen = [];
		events.Published += seen.Add;

		var result = await testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		var queued = await Assert.That(result).IsOfUnionType<EnqueueResult, Queued>();
		await Assert.That(queued.State).IsEqualTo(JobState.Queued);
		await Assert.That(seen.OfType<JobStateChangedEvent>().Single().State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task EnqueueAsync_WhenOnlyPlainManualRunsAreQueued_LetsTheArmedSyncWaitOutItsGrace()
	{
		var (testee, jobs, _) = BuildTestee();
		jobs.QueuedAsync(Any<UserId>(), JobKind.FeedRun, Any()).Returns([new RunFeed(FeedId.From(Guid.NewGuid()), new Manual())]);

		await testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		jobs.QueueWaitingAsync(Any<UserId>(), Any<JobKind>(), Any<DateTimeOffset>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task CancelAsync_ForAJobThatHasNotStarted_AnnouncesItCancelled()
	{
		var feedId = FeedId.From(Guid.NewGuid());
		IJobRequest request = new RunFeed(feedId, new Manual());
		var (testee, jobs, events) = BuildTestee();
		jobs.CancelAsync(Any<JobId>(), Any<UserId>(), Any<DateTimeOffset>(), Any()).Returns(Some.Of(request));
		List<IEvent> seen = [];
		events.Published += seen.Add;

		var cancelled = await testee.CancelAsync(JobId.From(Guid.NewGuid()));

		jobs.NextWaitingAsync(Any()).WasCalled(Times.Once);
		await Assert.That(cancelled).IsSome().And.IsEqualTo(request);
		await Assert
			.That(seen.OfType<JobStateChangedEvent>().Single())
			.IsEqualTo(new JobStateChangedEvent(TestCurrentUser.DefaultId, JobKind.FeedRun, feedId.ToString(), JobState.Cancelled));
	}

	[Test]
	public async Task CancelAsync_ForAJobThatAlreadyStarted_AnnouncesNothing()
	{
		var (testee, jobs, events) = BuildTestee();
		jobs.CancelAsync(Any<JobId>(), Any<UserId>(), Any<DateTimeOffset>(), Any()).Returns(new None());
		List<IEvent> seen = [];
		events.Published += seen.Add;

		var cancelled = await testee.CancelAsync(JobId.From(Guid.NewGuid()));

		jobs.NextWaitingAsync(Any()).WasCalled(Times.Never);
		await Assert.That(cancelled).IsNone();
		await Assert.That(seen).IsEmpty();
	}

	private static readonly FeedRunTrigger Nightly = new Scheduled("0 0 3 * * *");

	private static (JobQueue Testee, Mock<IJobRepository> Jobs, EventPublisher Events) BuildTestee()
	{
		var jobs = IJobRepository.Mock();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any<DateTimeOffset>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Queued));
		jobs.QueuedAsync(Any<UserId>(), Any<JobKind>(), Any()).Returns([]);
		jobs.QueueWaitingAsync(Any<UserId>(), Any<JobKind>(), Any<DateTimeOffset>(), Any()).Returns(0);
		jobs.NextWaitingAsync(Any()).Returns(new None());
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);

		var services = new ServiceCollection();
		services.AddSingleton(jobs.Object);
		var signal = new JobSignal();
		var alarm = new JobAlarm(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), signal, clock.Object, NullLogger<JobAlarm>.Instance);

		var events = new EventPublisher();
		return (
			new JobQueue(jobs.Object, signal, alarm, new TestCurrentUser(), clock.Object, Options, new Announcer(events, new RecordingPublisher())),
			jobs,
			events
		);
	}
}
