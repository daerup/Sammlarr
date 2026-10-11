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
using Domain.Usage;
using Domain.Wantlist.Sync;
using Observability.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Jobs;

public class FeedRunJobHandlerTests
{
	private static readonly FeedRunTrigger Nightly = new Scheduled("0 0 3 * * *");

	[Test]
	public async Task HandleAsync_ForAScheduledRun_ArmsASyncForTheOwner()
	{
		var (testee, queue, _, _) = Testee();

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), Nightly));

		queue.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task HandleAsync_ForAManualRunWithSyncAfter_ArmsASyncForTheOwner()
	{
		var (testee, queue, _, _) = Testee();

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual(), SyncAfter: true));

		queue.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task HandleAsync_ForAScheduledRun_ArmsTheSyncBeforeTheFeedRuns()
	{
		var (testee, queue, _, engine) = Testee();
		List<string> calls = [];
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Waiting)).Callback(() => calls.Add("arm"));
		engine.RunAsync(Any<FeedId>(), Any<FeedRunTrigger>(), Any()).Returns(new FeedRunFailed("stubbed")).Callback(() => calls.Add("run"));

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), Nightly));

		await Assert.That(calls.Take(2)).IsEquivalentTo(["arm", "run"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task HandleAsync_RunsTheFeedUnderItsFeedRunCause()
	{
		var id = FeedId.From(Guid.NewGuid());
		var (testee, _, _, engine) = Testee();
		var mark = new UsageMark(UsageCause.Other, new None());
		engine.RunAsync(Any<FeedId>(), Any<FeedRunTrigger>(), Any()).Returns(new FeedRunFailed("stubbed")).Callback(() => mark = UsageCauses.Current);

		await testee.HandleAsync(new RunFeed(id, new Manual()));

		await Assert.That(mark.Cause).IsEqualTo(UsageCause.FeedRun);
		await Assert.That(mark.Feed).IsSome().And.IsEqualTo(id);
	}

	[Test]
	public async Task HandleAsync_ForAPlainManualRun_ArmsNoSync()
	{
		var (testee, queue, _, _) = Testee();

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		queue.EnqueueAsync(Is<IJobRequest>(request => request is SyncWantlist), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task HandleAsync_AfterTheRun_QueuesAFillOfMissingDetails()
	{
		var (testee, queue, _, engine) = Testee();
		List<string> calls = [];
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Waiting)).Callback(() => calls.Add("queue"));
		engine.RunAsync(Any<FeedId>(), Any<FeedRunTrigger>(), Any()).Returns(new FeedRunFailed("stubbed")).Callback(() => calls.Add("run"));

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		await Assert.That(calls).IsEquivalentTo(["run", "queue"], CollectionOrdering.Matching);
		queue.EnqueueAsync(Is<IJobRequest>(request => request is FillDetails), Any()).WasCalled(Times.Once);
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
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid()), JobState.Queued));
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
		var events = new EventPublisher();
		var announcer = new Announcer(events, published ?? new RecordingPublisher());

		return (new FeedRunJobHandler(engine.Object, queue.Object, announcer, new TestCurrentUser(), clock.Object), queue, events, engine);
	}
}
