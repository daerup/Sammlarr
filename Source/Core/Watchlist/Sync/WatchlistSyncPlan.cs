using Contracts.Media;
using Contracts.Watchlist.Outbox;

namespace Core.Watchlist.Sync;

internal sealed record WatchlistSyncPlan(
	IReadOnlyList<IMediaId> InsertEntries,
	IReadOnlyList<IMediaId> DeleteEntries,
	IReadOnlyList<IMediaId> DropOutboxAdds,
	IReadOnlyList<IMediaId> DropOutboxRemoves,
	IReadOnlyList<IMediaId> PushAdds,
	IReadOnlyList<IMediaId> PushRemoves,
	int InSync
)
{
	public int InsertCount => InsertEntries.Count;
	public int ConfirmCount => DropOutboxAdds.Count;
	public int HardDeleteCount => DeleteEntries.Count + DropOutboxRemoves.Count;

	public static WatchlistSyncPlan Compute(IReadOnlyList<IMediaId> remoteIds, IReadOnlyList<IMediaId> localIds, IReadOnlyList<WatchlistOperation> outbox)
	{
		var remoteSet = remoteIds.ToHashSet();
		var localSet = localIds.ToHashSet();
		var outboxAdds = outbox.Where(o => o.Kind == WatchlistOperationKind.Add).Select(o => o.MediaId).ToList();
		var outboxRemoves = outbox.Where(o => o.Kind == WatchlistOperationKind.Remove).Select(o => o.MediaId).ToList();
		var outboxAddSet = outboxAdds.ToHashSet();
		var outboxRemoveSet = outboxRemoves.ToHashSet();

		return new WatchlistSyncPlan(
			InsertEntries: remoteIds.Where(id => !localSet.Contains(id) && !outboxRemoveSet.Contains(id)).ToList(),
			DeleteEntries: localIds.Where(id => !remoteSet.Contains(id) && !outboxAddSet.Contains(id)).ToList(),
			DropOutboxAdds: outboxAdds.Where(remoteSet.Contains).ToList(),
			DropOutboxRemoves: outboxRemoves.Where(id => !remoteSet.Contains(id)).ToList(),
			PushAdds: outboxAdds.Where(id => !remoteSet.Contains(id)).ToList(),
			PushRemoves: outboxRemoves.Where(remoteSet.Contains).ToList(),
			InSync: localIds.Count(id => remoteSet.Contains(id))
		);
	}
}
