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
[NotInParallel]
public class QueueBehaviourTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IJobQueue Testee => host.Resolve<IJobQueue>();

	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));
	private static readonly FeedId Anime = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d1"));

	[Test]
	public async Task RunningTwoDifferentFeeds_QueuesBoth()
	{
		await Testee.EnqueueAsync(new RunFeed(Docs, new Manual()));
		await Testee.EnqueueAsync(new RunFeed(Anime, new Manual()));

		await Assert.That(await PendingAsync()).IsEquivalentTo([$"FeedRun {Docs} Queued", $"FeedRun {Anime} Queued"]);
	}

	[Test]
	public async Task RunningTheSameFeedTwice_KeepsTheOneAlreadyQueued()
	{
		await Testee.EnqueueAsync(new RunFeed(Docs, new Manual()));

		var second = await Testee.EnqueueAsync(new RunFeed(Docs, new Manual()));

		await Assert.That(second).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(await PendingAsync()).IsEquivalentTo([$"FeedRun {Docs} Queued"]);
	}

	[Test]
	public async Task RunningAFeedThatIsAlreadyRunning_DoesNotQueueItAgain()
	{
		await Testee.EnqueueAsync(new RunFeed(Docs, new Manual()));
		await host.Resolve<IJobRepository>().TakeNextAsync(host.Resolve<IDateTimeProvider>().Now);

		var again = await Testee.EnqueueAsync(new RunFeed(Docs, new Manual()));

		await Assert.That(again).IsOfUnionType<EnqueueResult, AlreadyQueued>();
		await Assert.That(await PendingAsync()).IsEquivalentTo([$"FeedRun {Docs} Running"]);
	}

	[Test]
	public async Task RunningFeedsWithSyncAfter_QueuesOnlyTheRunsAndLetsEachRunArmTheSync()
	{
		await Testee.EnqueueAsync(new RunFeed(Docs, new Manual(), SyncAfter: true));

		await Assert.That(await PendingAsync()).IsEquivalentTo([$"FeedRun {Docs} Queued"]);
	}

	[Test]
	public async Task AFinishedRunThatWasAskedToSync_ArmsTheSyncWaitingRatherThanReadyToRun()
	{
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Assert.That(await PendingAsync()).IsEquivalentTo(["WatchlistSync Waiting"]);
	}

	[Test]
	public async Task ASecondRunFinishingInsideTheGrace_PushesTheWaitingSyncBackInsteadOfAddingAnother()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));
		host.SetCurrentTime(armedAt.AddSeconds(30));

		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		var gracePeriod = host.Resolve<SchedulerOptions>().Grace;
		await Assert.That(await PendingAsync()).IsEquivalentTo(["WatchlistSync Waiting"]);
		await Assert.That(await DueAsync()).IsEqualTo(armedAt.AddSeconds(30) + gracePeriod);
	}

	[Test]
	public async Task RunsThatKeepFinishing_StopPushingTheSyncOutOnceMaxWaitIsReached()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;
		var options = host.Resolve<SchedulerOptions>();
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));
		host.SetCurrentTime(armedAt + options.MaxWait + TimeSpan.FromHours(1));

		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Assert.That(await DueAsync()).IsEqualTo(armedAt + options.Grace + options.MaxWait);
	}

	[Test]
	public async Task PressingSyncWhileOneIsWaiting_PullsItForwardToRunNowInsteadOfAddingAnother()
	{
		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));

		await Assert.That(await PendingAsync()).IsEquivalentTo(["WatchlistSync Queued"]);
	}

	[Test]
	public async Task ARunFinishingWhileASyncIsRunning_ArmsAFollowUpSyncSoItsTitlesAreNotLost()
	{
		await Testee.EnqueueAsync(new SyncWatchlist(new ManualTrigger()));
		await host.Resolve<IJobRepository>().TakeNextAsync(host.Resolve<IDateTimeProvider>().Now);

		await Testee.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()));

		await Assert.That(await PendingAsync()).IsEquivalentTo(["WatchlistSync Running", "WatchlistSync Waiting"]);
	}

	private async Task<IReadOnlyList<string>> PendingAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		var pending = await context
			.Jobs.Where(row => row.State == JobState.Waiting || row.State == JobState.Queued || row.State == JobState.Running)
			.OrderBy(row => row.RunAfter)
			.Select(row => new
			{
				row.Kind,
				row.Key,
				row.State,
			})
			.ToListAsync();
		return [.. pending.Select(row => row.Key.Length == 0 ? $"{row.Kind} {row.State}" : $"{row.Kind} {row.Key} {row.State}")];
	}

	private async Task<DateTimeOffset> DueAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.OrderBy(row => row.RunAfter).Select(row => row.RunAfter).FirstAsync();
	}
}
