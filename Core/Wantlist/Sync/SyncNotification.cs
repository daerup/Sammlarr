using Domain.Announcing;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Wantlist.Outbox;

namespace Core.Wantlist.Sync;

internal static class SyncNotification
{
	internal static Optional<INotification> For(
		UserId owner,
		DateTimeOffset now,
		IReadOnlyList<MediaId> pulledAdds,
		IReadOnlyList<MediaId> pulledRemoves,
		IReadOnlyList<PushOutcome> pushed
	)
	{
		var pushedAdds = Delivered(pushed, WantlistOperationAction.Add);
		var pushedRemoves = Delivered(pushed, WantlistOperationAction.Remove);
		var failed = pushed.Where(outcome => outcome.Kind is not WantlistOutboxEventKind.Pushed).Select(outcome => outcome.MediaId).ToList();
		return pulledAdds.Count + pulledRemoves.Count + pushedAdds.Count + pushedRemoves.Count + failed.Count == 0
			? new None()
			: Some.Of<INotification>(new WantlistSyncCompletedNotification(owner, now, pulledAdds, pulledRemoves, pushedAdds, pushedRemoves, failed));
	}

	private static List<MediaId> Delivered(IReadOnlyList<PushOutcome> outcomes, WantlistOperationAction action) =>
		[.. outcomes.Where(outcome => outcome.Kind is WantlistOutboxEventKind.Pushed && outcome.Action == action).Select(outcome => outcome.MediaId)];
}
