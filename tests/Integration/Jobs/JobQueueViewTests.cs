using Contracts.Jobs;
using Domain.Feed;
using Domain.Identity;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Harness;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
[NotInParallel]
public class JobQueueViewTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IJobQueueView Testee => host.Resolve<IJobQueueView>();

	[Test]
	public async Task GetAsync_WhenNothingIsQueued_ReportsAnEmptyQueue()
	{
		var snapshot = await Testee.GetAsync();

		await Assert.That(snapshot.Jobs).IsEmpty();
		await Assert.That(snapshot.OthersPending).IsEqualTo(0);
	}

	[Test]
	public async Task GetAsync_ReturnsMyOwnJobsSoonestFirst()
	{
		var armedAt = host.Resolve<TimeProvider>().GetUtcNow();
		var feed = FeedId.From(Guid.NewGuid());
		await host.Resolve<IJobQueue>().EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()));
		await host.Resolve<IJobQueue>().EnqueueAsync(new RunFeed(feed, new Manual()));

		var snapshot = await Testee.GetAsync();

		await Assert.That(snapshot.Jobs.Select(job => job.Request.Kind)).IsEquivalentTo([JobKind.FeedRun, JobKind.WantlistSync]);
		await Assert.That(snapshot.Jobs[0].RunAfter).IsEqualTo(armedAt);
	}

	[Test]
	public async Task GetAsync_CountsAnotherUsersWorkAsOccupancyWithoutRevealingIt()
	{
		var mine = host.CurrentUser.Id;
		host.CurrentUser.Id = await host.SeedExistingUser("other-user", UserRole.Member, "Other User");
		await host.Resolve<IJobQueue>().EnqueueAsync(new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()));
		host.CurrentUser.Id = mine;

		var snapshot = await Testee.GetAsync();

		await Assert.That(snapshot.Jobs).IsEmpty();
		await Assert.That(snapshot.OthersPending).IsEqualTo(1);
	}
}
