using Contracts;
using Contracts.Feed;
using Contracts.Jobs;
using Core.Announcing;
using Core.Jobs;
using Domain.Announcing;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Notifications;
using Domain.Watchlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Jobs;

public class FeedRunJobHandlerTests
{
	private static readonly FeedRunTrigger Nightly = new Scheduled("0 0 3 * * *");

	[Test]
	public async Task HandleAsync_AfterAScheduledRun_ArmsASyncForTheOwner()
	{
		var (testee, queue, _, _) = Testee();

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), Nightly));

		queue.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task HandleAsync_AfterAManualRun_ArmsNothing()
	{
		var (testee, queue, _, _) = Testee();

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		queue.EnqueueAsync(Any<IJobRequest>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task HandleAsync_RunsTheFeedUnderTheTriggerItWasQueuedWith()
	{
		var id = FeedId.From(Guid.NewGuid());
		var (testee, _, _, engine) = Testee();

		await testee.HandleAsync(new RunFeed(id, Nightly));

		engine.RunAsync(id, Nightly, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task HandleAsync_AnnouncesTheRunFinishedSoAnOpenPageCanRefresh()
	{
		var id = FeedId.From(Guid.NewGuid());
		var (testee, _, events, _) = Testee();
		List<IEvent> seen = [];
		events.Published += seen.Add;

		await testee.HandleAsync(new RunFeed(id, new Manual()));

		var finished = seen.OfType<FeedRunFinishedEvent>().Single();
		await Assert.That(finished.FeedId).IsEqualTo(id);
		await Assert.That(finished.UserId).IsEqualTo(TestCurrentUser.DefaultId);
	}

	[Test]
	public async Task HandleAsync_PublishesTheRunsNotificationForDelivery()
	{
		var published = new RecordingPublisher();
		var (testee, _, _, _) = Testee(published);

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		await Assert.That(published.Published<FeedRunFailedNotification>()).Count().IsEqualTo(1);
	}

	private static (FeedRunJobHandler Testee, Mock<IJobQueue> Queue, EventPublisher Events, Mock<IFeedRunEngine> Engine) Testee(
		RecordingPublisher? published = null
	)
	{
		var engine = IFeedRunEngine.Mock();
		engine.RunAsync(Any<FeedId>(), Any<FeedRunTrigger>(), Any()).Returns(new FeedRunFailed("stubbed"));
		var queue = IJobQueue.Mock();
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid())));
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
		var events = new EventPublisher();
		var announcer = new Announcer(events, published ?? new RecordingPublisher());

		return (new FeedRunJobHandler(engine.Object, queue.Object, announcer, new TestCurrentUser(), clock.Object), queue, events, engine);
	}
}
