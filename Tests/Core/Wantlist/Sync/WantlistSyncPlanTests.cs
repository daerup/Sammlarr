using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;

namespace Core.Tests.Wantlist.Sync;

public class WantlistSyncPlanTests
{
	[Test]
	public async Task Compute_WhenTheRemoteHasAnIdTheLocalLacks_PullsItIn()
	{
		var remoteOnly = Id(1);
		var shared = Id(2);

		var plan = WantlistSyncPlan.Compute(Sink(remoteOnly, shared), Wanted(shared), Queued());

		await Assert.That(plan.Pull.Adds).IsEquivalentTo([remoteOnly]);
		await Assert.That(plan.Pull.Removes).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenTheLocalHasAnIdTheRemoteDropped_PullsARemoval()
	{
		var localOnly = Id(1);
		var shared = Id(2);

		var plan = WantlistSyncPlan.Compute(Sink(shared), Wanted(localOnly, shared), Queued());

		await Assert.That(plan.Pull.Removes).IsEquivalentTo([localOnly]);
		await Assert.That(plan.Pull.Adds).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOutboxEntryHasFailed_LeavesItOutOfThePush()
	{
		var abandoned = Id(1);

		var plan = WantlistSyncPlan.Compute(Sink(), Wanted(abandoned), Queued(Entry(abandoned, WantlistOperationAction.Add, WantlistOutboxState.Failed)));

		await Assert.That(plan.Push.Adds).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOutboxEntryHasFailed_StillCountsItAsIntentAgainstThePull()
	{
		var abandoned = Id(1);

		var plan = WantlistSyncPlan.Compute(Sink(), Wanted(abandoned), Queued(Entry(abandoned, WantlistOperationAction.Add, WantlistOutboxState.Failed)));

		await Assert.That(plan.Pull.Removes).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOutboxEntryIsPending_PushesIt()
	{
		var pending = Id(1);

		var plan = WantlistSyncPlan.Compute(Sink(), Wanted(pending), Queued(Entry(pending, WantlistOperationAction.Add, WantlistOutboxState.Pending)));

		await Assert.That(plan.Push.Adds).IsEquivalentTo([pending]);
	}

	[Test]
	public async Task Compute_CarriesTheTitlesTheSinkCouldNotMatch()
	{
		var unmatched = new UnmatchedMedia { Externals = [PlexId.From("plex-1", MediaType.Movie)], Title = "Solaris" };

		var plan = WantlistSyncPlan.Compute(new SinkSnapshot([], [unmatched]), Wanted(), Queued());

		await Assert.That(plan.Unmatched).IsEquivalentTo([unmatched]);
	}

	private static SinkSnapshot Sink(params MediaId[] ids) => new(ids, []);

	private static WantlistSnapshot Wanted(params MediaId[] ids) => WantlistStubs.Wanted(ids);

	private static OutboxSnapshot Queued(params WantlistOutboxEntry[] entries) => new(entries);

	private static WantlistOutboxEntry Entry(MediaId mediaId, WantlistOperationAction action, WantlistOutboxState status) => new(mediaId, action, status, 0);

	private static MediaId Id(int seed) => MediaId.From(new Guid(seed, 0, 0, new byte[8]));
}
