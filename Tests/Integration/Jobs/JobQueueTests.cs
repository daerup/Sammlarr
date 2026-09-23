using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class JobQueueTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IJobQueue Testee => host.Resolve<IJobQueue>();

	[Test]
	public async Task EnqueueAsync_WhenNothingIsQueued_QueuesTheJob()
	{
		var result = await Testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid())));

		await Assert.That(result).IsOfUnionType<EnqueueResult, Queued>();
	}

	[Test]
	public async Task EnqueueAsync_WhenTheSameFeedIsAlreadyQueued_FoldsIntoIt()
	{
		var request = new JobRequest(new RunFeed(FeedId.From(Guid.NewGuid())));

		var first = await Testee.EnqueueAsync(request);
		var second = await Testee.EnqueueAsync(request);

		var queued = await Assert.That(first).IsOfUnionType<EnqueueResult, Queued>();
		var folded = await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(folded!.Id).IsEqualTo(queued!.Id);
	}

	[Test]
	public async Task EnqueueAsync_WhenTheSameFeedIsEnqueuedConcurrently_QueuesItExactlyOnce()
	{
		var request = new JobRequest(new RunFeed(FeedId.From(Guid.NewGuid())));

		var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Testee.EnqueueAsync(request)));

		await Assert.That(results.Count(result => result is Queued)).IsEqualTo(1);
		await Assert.That(results.Count(result => result is AlreadyQueued)).IsEqualTo(7);
	}

	[Test]
	public async Task EnqueueAsync_WhenADifferentFeedIsQueued_QueuesItSeparately()
	{
		await Testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid())));

		var result = await Testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid())));

		await Assert.That(result).IsOfUnionType<EnqueueResult, Queued>();
	}

	[Test]
	public async Task EnqueueAsync_WhenAWatchlistSyncIsAlreadyQueued_FoldsIntoItBecauseTheOwnerIsTheKey()
	{
		var first = await Testee.EnqueueAsync(new SyncWatchlist());
		var second = await Testee.EnqueueAsync(new SyncWatchlist());

		var queued = await Assert.That(first).IsOfUnionType<EnqueueResult, Queued>();
		var folded = await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(folded!.Id).IsEqualTo(queued!.Id);
	}
}
