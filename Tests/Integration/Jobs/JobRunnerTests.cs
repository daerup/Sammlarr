using Contracts;
using Contracts.Jobs;
using Core.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
[NotInParallel]
public class JobRunnerTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private JobRunner Testee
	{
		get
		{
			var scopes = host.Resolve<IServiceScopeFactory>();
			var signal = new JobSignal();
			var clock = host.Resolve<IDateTimeProvider>();
			return new JobRunner(
				scopes,
				signal,
				new JobAlarm(scopes, signal, clock, NullLogger<JobAlarm>.Instance),
				new JobRunnerStatus(),
				clock,
				NullLogger<JobRunner>.Instance
			);
		}
	}

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
