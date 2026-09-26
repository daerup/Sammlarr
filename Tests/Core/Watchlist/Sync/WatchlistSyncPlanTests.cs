using Core.Watchlist.Sync;
using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Core.Tests.Watchlist.Sync;

public class WatchlistSyncPlanTests
{
	[Test]
	public async Task Compute_WhenTheRemoteHasAnIdTheLocalLacks_PullsItIn()
	{
		var remoteOnly = Id(1);
		var shared = Id(2);

		var plan = WatchlistSyncPlan.Compute([remoteOnly, shared], [shared], []);

		await Assert.That(plan.Pull.Adds).IsEquivalentTo([remoteOnly]);
		await Assert.That(plan.Pull.Removes).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenTheLocalHasAnIdTheRemoteDropped_PullsARemoval()
	{
		var localOnly = Id(1);
		var shared = Id(2);

		var plan = WatchlistSyncPlan.Compute([shared], [localOnly, shared], []);

		await Assert.That(plan.Pull.Removes).IsEquivalentTo([localOnly]);
		await Assert.That(plan.Pull.Adds).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOutboxEntryHasFailed_LeavesItOutOfThePush()
	{
		var abandoned = Id(1);

		var plan = WatchlistSyncPlan.Compute([], [abandoned], [Entry(abandoned, WatchlistOperationAction.Add, WatchlistOutboxState.Failed)]);

		await Assert.That(plan.Push.Adds).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOutboxEntryHasFailed_StillCountsItAsIntentAgainstThePull()
	{
		var abandoned = Id(1);

		var plan = WatchlistSyncPlan.Compute([], [abandoned], [Entry(abandoned, WatchlistOperationAction.Add, WatchlistOutboxState.Failed)]);

		await Assert.That(plan.Pull.Removes).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOutboxEntryIsPending_PushesIt()
	{
		var queued = Id(1);

		var plan = WatchlistSyncPlan.Compute([], [queued], [Entry(queued, WatchlistOperationAction.Add, WatchlistOutboxState.Pending)]);

		await Assert.That(plan.Push.Adds).IsEquivalentTo([queued]);
	}

	private static WatchlistOutboxEntry Entry(MediaId mediaId, WatchlistOperationAction action, WatchlistOutboxState status) => new(mediaId, action, status, 0);

	private static MediaId Id(int seed) => MediaId.From(new Guid(seed, 0, 0, new byte[8]));
}
