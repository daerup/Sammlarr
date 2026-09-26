using Contracts;
using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using Shared.Options;
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
		var result = await Testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		await Assert.That(result).IsOfUnionType<EnqueueResult, Queued>();
	}

	[Test]
	public async Task EnqueueAsync_WhenTheSameFeedIsAlreadyQueued_FoldsIntoIt()
	{
		IJobRequest request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());

		var first = await Testee.EnqueueAsync(request);
		var second = await Testee.EnqueueAsync(request);

		var queued = await Assert.That(first).IsOfUnionType<EnqueueResult, Queued>();
		var folded = await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(folded!.Id).IsEqualTo(queued!.Id);
	}

	[Test]
	public async Task EnqueueAsync_WhenTheSameFeedIsEnqueuedConcurrently_QueuesItExactlyOnce()
	{
		IJobRequest request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());

		var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Testee.EnqueueAsync(request)));

		await Assert.That(results.Count(result => result is Queued)).IsEqualTo(1);
		await Assert.That(results.Count(result => result is AlreadyQueued)).IsEqualTo(7);
	}

	[Test]
	public async Task EnqueueAsync_WhenADifferentFeedIsQueued_QueuesItSeparately()
	{
		await Testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		var result = await Testee.EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		await Assert.That(result).IsOfUnionType<EnqueueResult, Queued>();
	}

	[Test]
	public async Task EnqueueAsync_WhenAWatchlistSyncIsAlreadyQueued_FoldsIntoItBecauseTheOwnerIsTheKey()
	{
		var first = await Testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));
		var second = await Testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));

		var queued = await Assert.That(first).IsOfUnionType<EnqueueResult, Queued>();
		var folded = await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(folded!.Id).IsEqualTo(queued!.Id);
	}

	[Test]
	public async Task EnqueueAsync_WhenAScheduledSyncIsArmed_WaitsOutTheGraceBeforeItIsDue()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;

		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(armedAt + Options.Grace);
	}

	[Test]
	public async Task EnqueueAsync_WhenASyncIsRearmedInsideTheWindow_PushesTheGraceOut()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));
		host.SetCurrentTime(armedAt + TimeSpan.FromMinutes(1));

		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(armedAt + TimeSpan.FromMinutes(1) + Options.Grace);
	}

	[Test]
	public async Task EnqueueAsync_WhenASyncIsRearmedForever_StillFiresAtMaxWait()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));
		var ceiling = armedAt + Options.Grace + Options.MaxWait;

		foreach (var minute in new[] { 1, 31 })
		{
			host.SetCurrentTime(armedAt + TimeSpan.FromMinutes(minute));
			await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));
		}

		await Assert.That(await RunAfterAsync()).IsEqualTo(ceiling);
	}

	[Test]
	public async Task EnqueueAsync_WhenTheSameFeedIsRequeued_DoesNotDelayIt()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;
		IJobRequest request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());
		await Testee.EnqueueAsync(request);
		host.SetCurrentTime(armedAt + TimeSpan.FromMinutes(5));

		await Testee.EnqueueAsync(request);

		await Assert.That(await RunAfterAsync()).IsEqualTo(armedAt);
	}

	[Test]
	public async Task EnqueueAsync_WhenAManualSyncIsPressed_FiresImmediatelyWithoutTheGrace()
	{
		var pressedAt = host.Resolve<IDateTimeProvider>().Now;

		await Testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(pressedAt);
	}

	[Test]
	public async Task EnqueueAsync_WhenAManualSyncLandsOnAnArmedOne_PullsItForwardInsteadOfDeferringIt()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));
		var pressedAt = armedAt + TimeSpan.FromMinutes(1);
		host.SetCurrentTime(pressedAt);

		await Testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(pressedAt);
	}

	private SchedulerOptions Options => host.Resolve<SchedulerOptions>();

	private async Task<DateTimeOffset> RunAfterAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.Select(row => row.RunAfter).SingleAsync();
	}
}
