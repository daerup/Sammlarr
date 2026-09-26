using Contracts;
using Contracts.Jobs;
using Core.Jobs;
using DB.Data.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
[NotInParallel]
public class JobRunnerTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private JobRunner Testee =>
		new(host.Resolve<IServiceScopeFactory>(), new JobSignal(), new JobRunnerStatus(), host.Resolve<IDateTimeProvider>(), NullLogger<JobRunner>.Instance);

	[Test]
	public async Task RunNextAsync_WhenNothingIsQueued_DoesNothing()
	{
		await Assert.That(await Testee.TryRunNextAsync(CancellationToken.None)).IsFalse();
	}

	[Test]
	public async Task RunNextAsync_RunsAQueuedJob_AndMarksItDone()
	{
		await host.RemoteWatchlistEmpty();
		var queued = await Assert.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWatchlist(new ManualTrigger()))).IsOfUnionType<EnqueueResult, Queued>();
		PastTheGrace();

		await Assert.That(await Testee.TryRunNextAsync(CancellationToken.None)).IsTrue();

		await Assert.That(await StateOfAsync(queued!.Id)).IsEqualTo(JobState.Done);
	}

	[Test]
	public async Task RequeueRunningAsync_PutsAJobAPreviousProcessLeftRunningBackInTheQueue()
	{
		await host.RemoteWatchlistEmpty();
		var queued = await Assert.That(await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWatchlist(new ManualTrigger()))).IsOfUnionType<EnqueueResult, Queued>();
		PastTheGrace();
		await Assert.That(await host.Resolve<IJobRepository>().TakeNextAsync(host.Resolve<IDateTimeProvider>().Now)).IsSome();

		await Testee.RequeueRunningAsync(CancellationToken.None);

		await Assert.That(await StateOfAsync(queued!.Id)).IsEqualTo(JobState.Queued);
	}

	private void PastTheGrace() => host.SetCurrentTime(host.Resolve<IDateTimeProvider>().Now + TimeSpan.FromHours(1));

	private async Task<JobState> StateOfAsync(JobId id)
	{
		var raw = id.Value;
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.Where(row => row.Id == raw).Select(row => row.State).FirstAsync();
	}
}
