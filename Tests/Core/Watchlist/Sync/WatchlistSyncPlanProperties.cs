using Core.Watchlist.Sync;
using CsCheck;
using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Core.Tests.Watchlist.Sync;

public class WatchlistSyncPlanProperties
{
	private enum OutboxState
	{
		None,
		Add,
		Remove,
	}

	private sealed record Scenario(WatchlistSyncPlan Plan, int QueuedCount);

	private static readonly Gen<Scenario> Scenarios = Gen.Select(
			Gen.Bool,
			Gen.Bool,
			Gen.OneOfConst(OutboxState.None, OutboxState.Add, OutboxState.Remove),
			(remote, local, outbox) => (Remote: remote, Local: local, Outbox: outbox)
		)
		.Array[0, 6]
		.Select(Build);

	[Test]
	public void A_pull_never_adds_and_removes_the_same_id() => Scenarios.Sample(scenario => !scenario.Plan.Pull.Adds.Intersect(scenario.Plan.Pull.Removes).Any());

	[Test]
	public void The_plan_never_pushes_an_add_while_pulling_it_as_a_remove() =>
		Scenarios.Sample(scenario =>
			!scenario.Plan.Push.Adds.Intersect(scenario.Plan.Pull.Removes).Any() && !scenario.Plan.Push.Removes.Intersect(scenario.Plan.Pull.Adds).Any()
		);

	[Test]
	public void Every_queued_operation_is_classified_exactly_once() =>
		Scenarios.Sample(scenario => scenario.Plan.Push.Adds.Count + scenario.Plan.Push.Removes.Count + scenario.Plan.Redundant.Count == scenario.QueuedCount);

	private static Scenario Build((bool Remote, bool Local, OutboxState Outbox)[] situations)
	{
		var remote = new List<MediaId>();
		var local = new List<MediaId>();
		var outbox = new List<WatchlistOutboxEntry>();

		for (var index = 0; index < situations.Length; index++)
		{
			var id = MediaId.From(new Guid(index + 1, 0, 0, new byte[8]));
			var (inRemote, inLocal, state) = situations[index];

			if (inRemote)
			{
				remote.Add(id);
			}

			if (inLocal)
			{
				local.Add(id);
			}

			if (state is OutboxState.Add or OutboxState.Remove)
			{
				var action = state == OutboxState.Add ? WatchlistOperationAction.Add : WatchlistOperationAction.Remove;
				outbox.Add(new WatchlistOutboxEntry(id, action, WatchlistOutboxState.Pending, 0));
			}
		}

		return new Scenario(WatchlistSyncPlan.Compute(remote, local, outbox), outbox.Count);
	}
}
