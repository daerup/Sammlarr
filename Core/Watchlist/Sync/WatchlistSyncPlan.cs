using Domain.Media;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;

namespace Core.Watchlist.Sync;

internal sealed record WatchlistSyncPlan(WatchlistDelta Pull, WatchlistDelta Push, IReadOnlyList<MediaId> Redundant, IReadOnlyList<MediaId> NoOps)
{
	public IReadOnlyList<UnmatchedMedia> Unmatched { get; init; } = [];

	public static WatchlistSyncPlan Compute(IReadOnlyList<MediaId> remoteIds, IReadOnlyList<MediaId> localIds, IReadOnlyList<WatchlistOutboxEntry> outbox)
	{
		var remoteSet = remoteIds.ToHashSet();
		var localSet = localIds.ToHashSet();
		var outboxAdds = outbox.Where(o => o.Action == WatchlistOperationAction.Add).Select(o => o.MediaId).ToList();
		var outboxRemoves = outbox.Where(o => o.Action == WatchlistOperationAction.Remove).Select(o => o.MediaId).ToList();
		var outboxAddSet = outboxAdds.ToHashSet();
		var outboxRemoveSet = outboxRemoves.ToHashSet();
		var pendingAdds = PendingIds(outbox, WatchlistOperationAction.Add);
		var pendingRemoves = PendingIds(outbox, WatchlistOperationAction.Remove);

		return new WatchlistSyncPlan(
			Pull: new WatchlistDelta(
				Adds: remoteIds.Where(id => !localSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList(),
				Removes: localIds.Where(id => !remoteSet.Contains(id) && !outboxAddSet.Contains(id)).ToList()
			),
			Push: new WatchlistDelta(Adds: pendingAdds.Where(id => !remoteSet.Contains(id)).ToList(), Removes: pendingRemoves.Where(remoteSet.Contains).ToList()),
			Redundant: [.. outboxAdds.Where(remoteSet.Contains), .. outboxRemoves.Where(id => !remoteSet.Contains(id))],
			NoOps: localIds.Where(id => remoteSet.Contains(id) && !outboxAddSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList()
		);
	}

	private static List<MediaId> PendingIds(IReadOnlyList<WatchlistOutboxEntry> outbox, WatchlistOperationAction action) =>
		[.. outbox.Where(entry => entry.Status is WatchlistOutboxState.Pending && entry.Action == action).Select(entry => entry.MediaId)];
}
