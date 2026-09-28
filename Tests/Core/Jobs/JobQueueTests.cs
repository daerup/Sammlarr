using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Announcing;
using Core.Jobs;
using Domain.Announcing;
using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
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

		await testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		jobs.EnqueueAsync(new QueuedJob(new SyncWatchlist(new ScheduledFeedTrigger()), TestCurrentUser.DefaultId, Now + Options.Grace, Options.MaxWait), Now, Any())
			.WasCalled(Times.Once);
	}

	private static SchedulerOptions Options => new();

	[Test]
	public async Task EnqueueAsync_WhenItRunsNow_AnnouncesItQueued()
	{
		var (testee, _, events) = BuildTestee();
		List<IEvent> seen = [];
		events.Published += seen.Add;

		await testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));

		var announced = seen.OfType<JobStateChangedEvent>().Single();
		await Assert.That(announced.State).IsEqualTo(JobState.Queued);
		await Assert.That(announced.Kind).IsEqualTo(JobKind.WatchlistSync);
	}

	[Test]
	public async Task EnqueueAsync_AnnouncesTheStateTheRowWasActuallyWrittenWith_NotOneItGuessedFromTheDelay()
	{
		var (testee, jobs, events) = BuildTestee();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any<DateTimeOffset>(), Any()).Returns(new AlreadyQueued(JobId.From(Guid.NewGuid()), JobState.Queued));
		List<IEvent> seen = [];
		events.Published += seen.Add;

		await testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Assert.That(seen.OfType<JobStateChangedEvent>().Single().State).IsEqualTo(JobState.Queued);
	}

	private static (JobQueue Testee, Mock<IJobRepository> Jobs, EventPublisher Events) BuildTestee()
	{
		var jobs = IJobRepository.Mock();
		jobs.EnqueueAsync(Any<QueuedJob>(), Any<DateTimeOffset>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Queued));
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
