using Contracts.Media;
using Domain.Media;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;

namespace Core.Watchlist.Sync;

internal sealed record WatchlistSyncPlan(WatchlistDelta Pull, WatchlistDelta Push, WatchlistDelta Redundant, IReadOnlyList<MediaId> NoOps)
{
	public static WatchlistSyncPlan Compute(IReadOnlyList<MediaId> remoteIds, IReadOnlyList<MediaId> localIds, IReadOnlyList<WatchlistOperation> outbox)
	{
		var remoteSet = remoteIds.ToHashSet();
		var localSet = localIds.ToHashSet();
		var outboxAdds = outbox.Where(o => o.Action == WatchlistOperationAction.Add).Select(o => o.MediaId).ToList();
		var outboxRemoves = outbox.Where(o => o.Action == WatchlistOperationAction.Remove).Select(o => o.MediaId).ToList();
		var outboxAddSet = outboxAdds.ToHashSet();
		var outboxRemoveSet = outboxRemoves.ToHashSet();

		return new WatchlistSyncPlan(
			Pull: new WatchlistDelta(
				Adds: remoteIds.Where(id => !localSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList(),
				Removes: localIds.Where(id => !remoteSet.Contains(id) && !outboxAddSet.Contains(id)).ToList()
			),
			Push: new WatchlistDelta(Adds: outboxAdds.Where(id => !remoteSet.Contains(id)).ToList(), Removes: outboxRemoves.Where(remoteSet.Contains).ToList()),
			Redundant: new WatchlistDelta(Adds: outboxAdds.Where(remoteSet.Contains).ToList(), Removes: outboxRemoves.Where(id => !remoteSet.Contains(id)).ToList()),
			NoOps: localIds.Where(id => remoteSet.Contains(id) && !outboxAddSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList()
		);
	}
}
