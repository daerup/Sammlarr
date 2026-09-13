using Core.Watchlist.Sync;
using Domain.Media;

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

	private static MediaId Id(int seed) => MediaId.From(new Guid(seed, 0, 0, new byte[8]));
}
