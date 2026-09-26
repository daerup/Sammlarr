using Contracts.Feed;
using Contracts.Jobs;
using Core.Jobs;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Jobs;

public class FeedRunJobHandlerTests
{
	private static readonly FeedRunTrigger Nightly = new Scheduled("0 0 3 * * *");

	[Test]
	public async Task HandleAsync_AfterAScheduledRun_ArmsASyncForTheOwner()
	{
		var (engine, queue) = Collaborators();
		var testee = new FeedRunJobHandler(engine.Object, queue.Object);

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), Nightly));

		queue.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task HandleAsync_AfterAManualRun_ArmsNothing()
	{
		var (engine, queue) = Collaborators();
		var testee = new FeedRunJobHandler(engine.Object, queue.Object);

		await testee.HandleAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		queue.EnqueueAsync(Any<IJobRequest>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task HandleAsync_RunsTheFeedUnderTheTriggerItWasQueuedWith()
	{
		var id = FeedId.From(Guid.NewGuid());
		var (engine, queue) = Collaborators();
		var testee = new FeedRunJobHandler(engine.Object, queue.Object);

		await testee.HandleAsync(new RunFeed(id, Nightly));

		engine.RunAsync(id, Nightly, Any()).WasCalled(Times.Once);
	}

	private static (Mock<IFeedRunEngine> Engine, Mock<IJobQueue> Queue) Collaborators()
	{
		var engine = IFeedRunEngine.Mock();
		engine.RunAsync(Any<FeedId>(), Any<FeedRunTrigger>(), Any()).Returns(new FeedRunFailed("stubbed"));
		var queue = IJobQueue.Mock();
		queue.EnqueueAsync(Any<IJobRequest>(), Any()).Returns(new Queued(JobId.From(Guid.NewGuid())));
		return (engine, queue);
	}
}
