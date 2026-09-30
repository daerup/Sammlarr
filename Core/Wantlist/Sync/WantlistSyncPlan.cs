using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;

namespace Core.Wantlist.Sync;

internal sealed record WantlistSyncPlan(WantlistDelta Pull, WantlistDelta Push, IReadOnlyList<MediaId> Redundant, IReadOnlyList<MediaId> NoOps)
{
	public IReadOnlyList<UnmatchedMedia> Unmatched { get; init; } = [];

	public static WantlistSyncPlan Compute(IReadOnlyList<MediaId> remoteIds, IReadOnlyList<MediaId> localIds, IReadOnlyList<WantlistOutboxEntry> outbox)
	{
		var remoteSet = remoteIds.ToHashSet();
		var localSet = localIds.ToHashSet();
		var outboxAdds = outbox.Where(o => o.Action == WantlistOperationAction.Add).Select(o => o.MediaId).ToList();
		var outboxRemoves = outbox.Where(o => o.Action == WantlistOperationAction.Remove).Select(o => o.MediaId).ToList();
		var outboxAddSet = outboxAdds.ToHashSet();
		var outboxRemoveSet = outboxRemoves.ToHashSet();
		var pendingAdds = PendingIds(outbox, WantlistOperationAction.Add);
		var pendingRemoves = PendingIds(outbox, WantlistOperationAction.Remove);

		return new WantlistSyncPlan(
			Pull: new WantlistDelta(
				Adds: remoteIds.Where(id => !localSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList(),
				Removes: localIds.Where(id => !remoteSet.Contains(id) && !outboxAddSet.Contains(id)).ToList()
			),
			Push: new WantlistDelta(Adds: pendingAdds.Where(id => !remoteSet.Contains(id)).ToList(), Removes: pendingRemoves.Where(remoteSet.Contains).ToList()),
			Redundant: [.. outboxAdds.Where(remoteSet.Contains), .. outboxRemoves.Where(id => !remoteSet.Contains(id))],
			NoOps: localIds.Where(id => remoteSet.Contains(id) && !outboxAddSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList()
		);
	}

	private static List<MediaId> PendingIds(IReadOnlyList<WantlistOutboxEntry> outbox, WantlistOperationAction action) =>
		[.. outbox.Where(entry => entry.Status is WantlistOutboxState.Pending && entry.Action == action).Select(entry => entry.MediaId)];
}
