using Domain.Announcing;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Watchlist.Outbox;

namespace Core.Watchlist.Sync;

internal static class SyncNotification
{
	internal static INotification? For(
		UserId owner,
		DateTimeOffset now,
		IReadOnlyList<MediaId> pulledAdds,
		IReadOnlyList<MediaId> pulledRemoves,
		IReadOnlyList<PushOutcome> pushed
	)
	{
		var pushedAdds = Delivered(pushed, WatchlistOperationAction.Add);
		var pushedRemoves = Delivered(pushed, WatchlistOperationAction.Remove);
		var failed = pushed.Where(outcome => outcome.Kind is not WatchlistOutboxEventKind.Pushed).Select(outcome => outcome.MediaId).ToList();
		return pulledAdds.Count + pulledRemoves.Count + pushedAdds.Count + pushedRemoves.Count + failed.Count == 0
			? null
			: new WatchlistSyncCompletedNotification(owner, now, pulledAdds, pulledRemoves, pushedAdds, pushedRemoves, failed);
	}

	private static List<MediaId> Delivered(IReadOnlyList<PushOutcome> outcomes, WatchlistOperationAction action) =>
		[.. outcomes.Where(outcome => outcome.Kind is WatchlistOutboxEventKind.Pushed && outcome.Action == action).Select(outcome => outcome.MediaId)];
}
