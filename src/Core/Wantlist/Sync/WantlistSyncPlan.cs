using Domain.Media;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;

namespace Core.Wantlist.Sync;

internal sealed record WantlistSyncPlan(
	WantlistDelta Pull,
	WantlistDelta Push,
	IReadOnlyList<MediaId> Redundant,
	IReadOnlyList<MediaId> NoOps,
	IReadOnlyList<UnmatchedMedia> Unmatched
)
{
	public static WantlistSyncPlan Compute(SinkSnapshot sink, WantlistSnapshot wantlist, OutboxSnapshot outbox) =>
		new(
			Pull: new WantlistDelta(
				Adds: [.. sink.Ids.Where(id => !wantlist.Contains(id) && !outbox.Removes.ContainsKey(id))],
				Removes: [.. wantlist.Ids.Where(id => !sink.Contains(id) && !outbox.Adds.ContainsKey(id))]
			),
			Push: new WantlistDelta(
				Adds: [.. PendingIds(outbox.Adds).Where(id => !sink.Contains(id))],
				Removes: [.. PendingIds(outbox.Removes).Where(sink.Contains)]
			),
			Redundant: [.. outbox.Adds.Keys.Where(sink.Contains), .. outbox.Removes.Keys.Where(id => !sink.Contains(id))],
			NoOps: [.. wantlist.Ids.Where(id => sink.Contains(id) && !outbox.Adds.ContainsKey(id) && !outbox.Removes.ContainsKey(id))],
			Unmatched: sink.Unmatched
		);

	private static IEnumerable<MediaId> PendingIds(IReadOnlyDictionary<MediaId, WantlistOutboxEntry> queued) =>
		queued.Values.Where(entry => entry.Status is WantlistOutboxState.Pending).Select(entry => entry.MediaId);
}
