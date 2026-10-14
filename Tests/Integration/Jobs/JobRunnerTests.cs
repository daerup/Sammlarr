using Contracts;
using Contracts.Feed;
using Contracts.Jobs;
using Core.Jobs;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Jobs;
using Domain.Providers;
using Domain.Wantlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using Shared.Options;
using TestInfrastructure;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
[NotInParallel]
public class JobRunnerTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private JobRunner Testee => host.BuildJobRunner();

	[Test]
	public async Task RunNextAsync_WhenNothingIsQueued_DoesNothing()
	{
		await Assert.That(await Testee.TryRunNextAsync(CancellationToken.None)).IsFalse();
	}

	[Test]
	public async Task RunNextAsync_RunsAQueuedJob_AndMarksItDone()
	{
		await host.RemoteWantlistEmpty();
		var queued = await Assert.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ManualTrigger()))).IsOfUnionType<EnqueueResult, Queued>();
		PastTheGrace();

		await Assert.That(await Testee.TryRunNextAsync(CancellationToken.None)).IsTrue();

		await Assert.That(await StateOfAsync(queued.Id)).IsEqualTo(JobState.Done);
	}

	[Test]
	public async Task RunNextAsync_WhileTheTurnIsHeld_StartsTheJobOnlyOnceItIsReleased()
	{
		await host.RemoteWantlistEmpty();
		var queued = await Assert.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ManualTrigger()))).IsOfUnionType<EnqueueResult, Queued>();
		PastTheGrace();
		var held = await host.Resolve<JobTurn>().TakeAsync(CancellationToken.None);

		var running = Testee.TryRunNextAsync(CancellationToken.None);
		await Task.Delay(TimeSpan.FromMilliseconds(200));
		var stateWhileHeld = await StateOfAsync(queued.Id);
		held.Dispose();

		await Assert.That(await running).IsTrue();
		await Assert.That(stateWhileHeld).IsEqualTo(JobState.Queued);
		await Assert.That(await StateOfAsync(queued.Id)).IsEqualTo(JobState.Done);
	}

	[Test]
	public async Task EnqueueAsync_ForAScheduledSync_LeavesItWaitingWithItsDueTimeRatherThanReadyToRun()
	{
		var armedAt = host.Resolve<IDateTimeProvider>().Now;

		var queued = await Assert
			.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger())))
			.IsOfUnionType<EnqueueResult, Queued>();

		await Assert.That(await StateOfAsync(queued.Id)).IsEqualTo(JobState.Waiting);
		var pending = (await host.Resolve<IJobQueueView>().GetAsync()).Jobs.Single();
		await Assert.That(pending.State).IsEqualTo(JobState.Waiting);
		await Assert.That(pending.RunAfter).IsEqualTo(armedAt + host.Resolve<SchedulerOptions>().Grace);
	}

	[Test]
	public async Task EnqueueAsync_ForTwoScheduledRuns_StillLeavesOneWaitingSyncRatherThanTwo()
	{
		await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));
		await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		await Assert.That(await KindsAsync()).IsEquivalentTo([JobKind.WantlistSync]);
	}

	[Test]
	public async Task EnqueueAsync_WhenAManualSyncLandsOnAWaitingOne_PullsItForwardToQueuedInsteadOfAddingASecond()
	{
		await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));

		var pressed = await Assert
			.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ManualTrigger())))
			.IsOfUnionType<EnqueueResult, AlreadyQueued>();

		await Assert.That(await StateOfAsync(pressed.Id)).IsEqualTo(JobState.Queued);
		await Assert.That(await KindsAsync()).IsEquivalentTo([JobKind.WantlistSync]);
	}

	[Test]
	public async Task ReleaseDueAsync_OnceTheGraceHasPassed_MakesTheWaitingSyncTakeableAndItRunsToDone()
	{
		await host.RemoteWantlistEmpty();
		var queued = await Assert
			.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger())))
			.IsOfUnionType<EnqueueResult, Queued>();
		PastTheGrace();

		await host.Resolve<IJobRepository>().ReleaseDueAsync(host.Resolve<IDateTimeProvider>().Now);

		await Assert.That(await Testee.TryRunNextAsync(CancellationToken.None)).IsTrue();
		await Assert.That(await StateOfAsync(queued.Id)).IsEqualTo(JobState.Done);
	}

	[Test]
	public async Task RequeueRunningAsync_PutsAJobAPreviousProcessLeftRunningBackInTheQueue()
	{
		await host.RemoteWantlistEmpty();
		var queued = await Assert.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ManualTrigger()))).IsOfUnionType<EnqueueResult, Queued>();
		PastTheGrace();
		await Assert.That(await host.Resolve<IJobRepository>().TakeNextAsync(host.Resolve<IDateTimeProvider>().Now)).IsSome();

		await Testee.RequeueRunningAsync(CancellationToken.None);

		await Assert.That(await StateOfAsync(queued.Id)).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task RunNextAsync_AfterAManualFeedRun_ArmsNothingSoAnIdleAppStaysSilent()
	{
		await host.Resolve<IJobQueue>().EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));

		await Testee.TryRunNextAsync(CancellationToken.None);

		await Assert.That(await KindsAsync()).DoesNotContain(JobKind.WantlistSync);
	}

	[Test]
	public async Task RunNextAsync_ForAQueuedScheduledFeedRun_StoresTheRunUnderTheScheduledTrigger()
	{
		var feed = await SeedFeedAsync("nightly-list");
		host.MdblistFeedEmpty("nightly-list");
		await host.Resolve<IJobQueue>().EnqueueAsync(new RunFeed(feed, new Scheduled("0 0 3 * * *")));

		await Testee.TryRunNextAsync(CancellationToken.None);

		var run = (await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feed) })).Single();
		await Assert.That(run.Trigger).IsOfUnionType<FeedRunTrigger, Scheduled>();
	}

	private async Task<FeedId> SeedFeedAsync(string identifier)
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var entity = new FeedRow
		{
			Name = "Nightly",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}

	private async Task<IReadOnlyList<JobKind>> KindsAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.Select(row => row.Kind).ToListAsync();
	}

	private void PastTheGrace() => host.SetCurrentTime(host.Resolve<IDateTimeProvider>().Now + TimeSpan.FromHours(1));

	private async Task<JobState> StateOfAsync(JobId id)
	{
		var raw = id.Value;
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.Where(row => row.Id == raw).Select(row => row.State).FirstAsync();
	}
}
