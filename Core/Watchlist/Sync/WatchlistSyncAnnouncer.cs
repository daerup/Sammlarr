using Contracts;
using Contracts.Announcing;
using Contracts.Identity;
using Contracts.Messaging;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Watchlist.Outbox;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncAnnouncer(IEventPublisher events, IMessagePublisher publisher, IDateTimeProvider clock, ICurrentUser currentUser)
{
	internal void NotifyPushed(PushOutcome outcome) => events.Publish(new WatchlistOutboxEvent(currentUser.Id, outcome.MediaId, outcome.Kind));

	internal async Task NotifySyncedAsync(
		IReadOnlyList<MediaId> pulledAdds,
		IReadOnlyList<MediaId> pulledRemoves,
		IReadOnlyList<PushOutcome> pushed,
		CancellationToken cancellationToken
	)
	{
		var pushedAdds = Delivered(pushed, WatchlistOperationAction.Add);
		var pushedRemoves = Delivered(pushed, WatchlistOperationAction.Remove);
		var failed = pushed.Where(outcome => outcome.Kind is not WatchlistOutboxEventKind.Pushed).Select(outcome => outcome.MediaId).ToList();
		if (pulledAdds.Count + pulledRemoves.Count + pushedAdds.Count + pushedRemoves.Count + failed.Count == 0)
		{
			return;
		}

		await publisher.PublishAsync(
			new WatchlistSyncedEvent(currentUser.Id, clock.Now, pulledAdds, pulledRemoves, pushedAdds, pushedRemoves, failed),
			cancellationToken
		);
	}

	internal async Task NotifySyncFailedAsync(string reason, CancellationToken cancellationToken) =>
		await publisher.PublishAsync(new WatchlistSyncFailedEvent(currentUser.Id, clock.Now, reason), cancellationToken);

	private static List<MediaId> Delivered(IReadOnlyList<PushOutcome> outcomes, WatchlistOperationAction action) =>
		[.. outcomes.Where(outcome => outcome.Kind is WatchlistOutboxEventKind.Pushed && outcome.Action == action).Select(outcome => outcome.MediaId)];
}
