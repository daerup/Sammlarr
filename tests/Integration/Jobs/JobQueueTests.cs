using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Wantlist.Sync;
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
	public async Task EnqueueAsync_WhenTheSameFeedIsAlreadyQueued_FoldsIntoIt()
	{
		IJobRequest request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());

		var first = await Testee.EnqueueAsync(request);
		var second = await Testee.EnqueueAsync(request);

		var queued = await Assert.That(first).IsOfUnionType<EnqueueResult, Queued>();
		var folded = await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(folded.Id).IsEqualTo(queued.Id);
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
	public async Task EnqueueAsync_WhenAWantlistSyncIsAlreadyQueued_FoldsIntoItBecauseTheOwnerIsTheKey()
	{
		var first = await Testee.EnqueueAsync(new SyncWantlist(new ManualTrigger()));
		var second = await Testee.EnqueueAsync(new SyncWantlist(new ManualTrigger()));

		var queued = await Assert.That(first).IsOfUnionType<EnqueueResult, Queued>();
		var folded = await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(folded.Id).IsEqualTo(queued.Id);
	}

	[Test]
	public async Task EnqueueAsync_WhenAScheduledSyncIsArmed_WaitsOutTheGraceBeforeItIsDue()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();

		await Testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(armedAt + Options.Grace);
	}

	[Test]
	public async Task EnqueueAsync_WhenASyncIsRearmedInsideTheWindow_PushesTheGraceOut()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();
		await Testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));
		host.SetCurrentTime(armedAt + TimeSpan.FromMinutes(1));

		await Testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(armedAt + TimeSpan.FromMinutes(1) + Options.Grace);
	}

	[Test]
	public async Task EnqueueAsync_WhenASyncIsRearmedForever_StillFiresAtMaxWait()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();
		await Testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));
		var ceiling = armedAt + Options.Grace + Options.MaxWait;

		foreach (var minute in new[] { 1, 31 })
		{
			host.SetCurrentTime(armedAt + TimeSpan.FromMinutes(minute));
			await Testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));
		}

		await Assert.That(await RunAfterAsync()).IsEqualTo(ceiling);
	}

	[Test]
	public async Task EnqueueAsync_WhenTheSameFeedIsRequeued_DoesNotDelayIt()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();
		IJobRequest request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());
		await Testee.EnqueueAsync(request);
		host.SetCurrentTime(armedAt + TimeSpan.FromMinutes(5));

		await Testee.EnqueueAsync(request);

		await Assert.That(await RunAfterAsync()).IsEqualTo(armedAt);
	}

	[Test]
	public async Task EnqueueAsync_WhenAManualSyncIsPressed_FiresImmediatelyWithoutTheGrace()
	{
		var pressedAt = host.Resolve<TimeProvider>().GetUtcNow();

		await Testee.EnqueueAsync(new SyncWantlist(new ManualTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(pressedAt);
	}

	[Test]
	public async Task EnqueueAsync_WhenAManualSyncLandsOnAnArmedOne_PullsItForwardInsteadOfDeferringIt()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();
		await Testee.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));
		var pressedAt = armedAt + TimeSpan.FromMinutes(1);
		host.SetCurrentTime(pressedAt);

		await Testee.EnqueueAsync(new SyncWantlist(new ManualTrigger()));

		await Assert.That(await RunAfterAsync()).IsEqualTo(pressedAt);
	}

	[Test]
	public async Task TakeNextAsync_ForAFillDetailsWhileAFeedRunIsQueued_TakesTheFeedRun()
	{
		var (run, dueAt) = await FillThenRunAsync();

		var taken = await host.Resolve<IJobRepository>().TakeNextAsync(dueAt + TimeSpan.FromMinutes(1));

		await Assert.That(taken.Map(job => job.Request)).IsSome().And.IsEqualTo(run);
	}

	[Test]
	public async Task TakeNextAsync_ForAFillDetailsWhileAFeedRunIsQueued_AfterMaxWait_TakesTheFill()
	{
		var (_, dueAt) = await FillThenRunAsync();

		var taken = await host.Resolve<IJobRepository>().TakeNextAsync(dueAt + Options.FillDetailsMaxWait);

		await Assert.That(taken.Map(job => job.Request)).IsSome().And.IsEqualTo(new FillDetails());
	}

	[Test]
	public async Task RequeueRunningAsync_ForARunningJobWithAPendingTwin_CancelsItAndRequeuesTheRest()
	{
		var jobs = host.Resolve<IJobRepository>();
		var now = host.Resolve<TimeProvider>().GetUtcNow();
		var (run, _) = await FillThenRunAsync();
		var later = now + Options.FillDetailsGrace + TimeSpan.FromMinutes(1);
		var fill = await jobs.TakeNextAsync(later + Options.FillDetailsMaxWait);
		var feed = await jobs.TakeNextAsync(later + Options.FillDetailsMaxWait);
		await Testee.EnqueueAsync(new FillDetails());

		var requeued = await jobs.RequeueRunningAsync(later);

		await Assert.That(fill.Map(job => job.Request)).IsSome().And.IsEqualTo(new FillDetails());
		await Assert.That(feed.Map(job => job.Request)).IsSome().And.IsEqualTo(run);
		await Assert.That(requeued).IsEqualTo(1);
		await Assert
			.That(await StatesAsync())
			.IsEquivalentTo([(JobKind.DetailsFill, JobState.Cancelled), (JobKind.DetailsFill, JobState.Waiting), (JobKind.FeedRun, JobState.Queued)]);
	}

	private async Task<IReadOnlyList<(JobKind Kind, JobState State)>> StatesAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return [.. (await context.Jobs.AsNoTracking().ToListAsync()).Select(row => (row.Kind, row.State))];
	}

	private async Task<(IJobRequest Run, DateTimeOffset DueAt)> FillThenRunAsync()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();
		await Testee.EnqueueAsync(new FillDetails());
		var dueAt = armedAt + Options.FillDetailsGrace;
		host.SetCurrentTime(dueAt);
		await host.Resolve<IJobRepository>().ReleaseDueAsync(dueAt);
		host.SetCurrentTime(dueAt + TimeSpan.FromMinutes(1));
		IJobRequest run = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());
		await Testee.EnqueueAsync(run);
		return (run, dueAt);
	}

	private SchedulerOptions Options => host.Resolve<SchedulerOptions>();

	private async Task<DateTimeOffset> RunAfterAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.Select(row => row.RunAfter).SingleAsync();
	}
}
