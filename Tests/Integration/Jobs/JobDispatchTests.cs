using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
[NotInParallel]
public class JobDispatchTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IJobRepository Testee => host.Resolve<IJobRepository>();

	[Before(Test)]
	public async Task EmptyTheQueue()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		await context.Jobs.ExecuteDeleteAsync();
	}

	[Test]
	public async Task TakeNextAsync_WhenNothingIsQueued_TakesNothing()
	{
		await Assert.That(await Testee.TakeNextAsync(Now)).IsNone();
	}

	[Test]
	public async Task TakeNextAsync_TakesTheQueuedJobWithItsOwner()
	{
		var feedId = FeedId.From(Guid.NewGuid());
		await EnqueueAsync(feedId);

		var taken = await Assert.That(await Testee.TakeNextAsync(Now)).IsSome();

		await Assert.That(taken!.Kind).IsEqualTo(JobKind.FeedRun);
		await Assert.That(taken!.Key).IsEqualTo(feedId.Value.ToString());
		await Assert.That(taken!.Owner.Value).IsEqualTo(SammlarrTestHost.SeededAdmin.Id);
	}

	[Test]
	public async Task TakeNextAsync_WhenAJobIsAlreadyRunning_DoesNotHandItOutTwice()
	{
		await EnqueueAsync(FeedId.From(Guid.NewGuid()));

		await Assert.That(await Testee.TakeNextAsync(Now)).IsSome();
		await Assert.That(await Testee.TakeNextAsync(Now)).IsNone();
	}

	[Test]
	public async Task TakeNextAsync_WhenTheJobIsNotDueYet_TakesNothing()
	{
		await EnqueueAsync(FeedId.From(Guid.NewGuid()));

		await Assert.That(await Testee.TakeNextAsync(Now.AddMinutes(-1))).IsNone();
	}

	[Test]
	public async Task TakeNextAsync_WhenTheJobIsComplete_TakesNothing()
	{
		await EnqueueAsync(FeedId.From(Guid.NewGuid()));
		var taken = await Assert.That(await Testee.TakeNextAsync(Now)).IsSome();

		await Testee.CompleteAsync(taken!.Id, Now);

		await Assert.That(await Testee.TakeNextAsync(Now.AddHours(1))).IsNone();
	}

	[Test]
	public async Task TakeNextAsync_TakesTheJobThatBecameDueFirst()
	{
		var later = FeedId.From(Guid.NewGuid());
		var earlier = FeedId.From(Guid.NewGuid());
		await EnqueueAsync(later, Now.AddMinutes(1));
		await EnqueueAsync(earlier, Now);

		var taken = await Assert.That(await Testee.TakeNextAsync(Now.AddMinutes(2))).IsSome();

		await Assert.That(taken!.Key).IsEqualTo(earlier.Value.ToString());
	}

	[Test]
	public async Task RequeueRunningAsync_PutsAJobTheProcessDiedHoldingBackInTheQueue()
	{
		await EnqueueAsync(FeedId.From(Guid.NewGuid()));
		await Testee.TakeNextAsync(Now);

		await Assert.That(await Testee.RequeueRunningAsync(Now)).IsEqualTo(1);

		await Assert.That(await Testee.TakeNextAsync(Now)).IsSome();
	}

	[Test]
	public async Task RequeueRunningAsync_CountsTheLostRunAgainstTheJob()
	{
		await EnqueueAsync(FeedId.From(Guid.NewGuid()));
		var taken = await Assert.That(await Testee.TakeNextAsync(Now)).IsSome();

		await Testee.RequeueRunningAsync(Now);

		await Assert.That(await AttemptsOfAsync(taken!.Id)).IsEqualTo(1);
	}

	[Test]
	public async Task RequeueRunningAsync_LeavesFinishedJobsAlone()
	{
		await EnqueueAsync(FeedId.From(Guid.NewGuid()));
		var taken = await Assert.That(await Testee.TakeNextAsync(Now)).IsSome();
		await Testee.CompleteAsync(taken!.Id, Now);

		await Assert.That(await Testee.RequeueRunningAsync(Now)).IsEqualTo(0);
	}

	private async Task EnqueueAsync(FeedId feedId) => await EnqueueAsync(feedId, Now);

	private async Task EnqueueAsync(FeedId feedId, DateTimeOffset at)
	{
		host.SetCurrentTime(at);
		await host.Resolve<IJobQueue>().EnqueueAsync(new RunFeed(feedId));
	}

	private async Task<int> AttemptsOfAsync(JobId id)
	{
		var raw = id.Value;
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.Where(row => row.Id == raw).Select(row => row.Attempts).FirstAsync();
	}
}
