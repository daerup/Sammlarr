using Contracts.Media;
using Contracts.Notifications;
using Domain.Media;
using Domain.Notifications.Events;

namespace Messaging.Notifications;

internal static class NotificationContextFactory
{
	public static async Task<NotificationContext> BuildAsync(BaseNotification message, IMediaRepository mediaRepository, CancellationToken cancellationToken)
	{
		var payload = ToPayload(message);
		var resolved = await ResolveAsync(payload, mediaRepository, cancellationToken);
		return Build(payload, resolved);
	}

	private static NotificationPayload ToPayload(BaseNotification message) =>
		message switch
		{
			WatchlistSyncedEvent e => Payload(
				e,
				"Watchlist synced",
				$"Pulled {e.PulledAdds.Count + e.PulledRemoves.Count}, queued {e.PushedAdds.Count + e.PushedRemoves.Count} for push.",
				pulledAdds: e.PulledAdds,
				pulledRemoves: e.PulledRemoves,
				pushedAdds: e.PushedAdds,
				pushedRemoves: e.PushedRemoves
			),
			WatchlistItemsAddedEvent e => Payload(e, "Watchlist items added", $"Added {e.MediaIds.Count} item(s).", media: e.MediaIds),
			WatchlistItemsRemovedEvent e => Payload(e, "Watchlist items removed", $"Removed {e.MediaIds.Count} item(s).", media: e.MediaIds),
			TestPingEvent e => Payload(e, "Test notification", "This is a test notification from Sammlarr."),
			FeedItemsAddedEvent e => Payload(e, "Feed items added", $"Added {e.MediaIds.Count} item(s).", media: e.MediaIds) with { FeedId = e.FeedId.ToString() },
			FeedItemsRemovedEvent e => Payload(e, "Feed items removed", $"Removed {e.MediaIds.Count} item(s).", media: e.MediaIds) with
			{
				FeedId = e.FeedId.ToString(),
			},
			FeedRunCompletedEvent e => Payload(e, "Feed run completed", $"Added {e.Added}, removed {e.Removed}.") with
			{
				FeedId = e.FeedId.ToString(),
				Added = e.Added,
				Removed = e.Removed,
			},
			FeedRunFailedEvent e => Payload(e, "Feed run failed", e.Reason) with { FeedId = e.FeedId.ToString(), Reason = e.Reason },
			_ => Payload(message, message.Type, string.Empty),
		};

	private static async Task<IReadOnlyDictionary<MediaId, NotificationMedia>> ResolveAsync(
		NotificationPayload payload,
		IMediaRepository mediaRepository,
		CancellationToken cancellationToken
	)
	{
		var ids = payload.AllMediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return NoMedia;
		}
		var cached = await mediaRepository.GetAsync(ids, cancellationToken);
		return cached.ToDictionary(
			entry => entry.Stored.Id,
			entry => new NotificationMedia(entry.Stored.Id.ToString(), entry.Stored.Title, entry.Stored.Type.ToString())
		);
	}

	private static NotificationContext Build(NotificationPayload payload, IReadOnlyDictionary<MediaId, NotificationMedia> resolved) =>
		new(
			payload.Id,
			payload.Type,
			payload.OccurredAt,
			payload.Title,
			payload.Summary,
			new NotificationData
			{
				PulledAdds = Lookup(payload.PulledAdds, resolved),
				PulledRemoves = Lookup(payload.PulledRemoves, resolved),
				PushedAdds = Lookup(payload.PushedAdds, resolved),
				PushedRemoves = Lookup(payload.PushedRemoves, resolved),
				Media = Lookup(payload.Media, resolved),
				FeedId = payload.FeedId,
				Added = payload.Added,
				Removed = payload.Removed,
				Reason = payload.Reason,
			}
		);

	private static readonly IReadOnlyDictionary<MediaId, NotificationMedia> NoMedia = new Dictionary<MediaId, NotificationMedia>();

	private static NotificationPayload Payload(
		BaseNotification message,
		string title,
		string summary,
		IReadOnlyList<MediaId>? pulledAdds = null,
		IReadOnlyList<MediaId>? pulledRemoves = null,
		IReadOnlyList<MediaId>? pushedAdds = null,
		IReadOnlyList<MediaId>? pushedRemoves = null,
		IReadOnlyList<MediaId>? media = null
	) =>
		new(
			message.Id,
			message.Type,
			message.OccurredAt,
			title,
			summary,
			pulledAdds ?? [],
			pulledRemoves ?? [],
			pushedAdds ?? [],
			pushedRemoves ?? [],
			media ?? [],
			null,
			null,
			null,
			null
		);

	private static IReadOnlyList<NotificationMedia>? Lookup(IReadOnlyList<MediaId> ids, IReadOnlyDictionary<MediaId, NotificationMedia> resolved) =>
		ids.Count == 0
			? null
			: ids.Select(id => resolved.GetValueOrDefault(id) ?? new NotificationMedia(id.ToString(), null, MediaType.Unknown.ToString())).ToList();
}
