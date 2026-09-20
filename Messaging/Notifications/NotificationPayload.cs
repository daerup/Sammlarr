using Domain.Media;
using Domain.Notifications;
using Domain.Providers;

namespace Messaging.Notifications;

internal sealed record NotificationPayload(
	Guid Id,
	NotificationEventType Type,
	DateTimeOffset OccurredAt,
	string Title,
	string Summary,
	IReadOnlyList<MediaId> PulledAdds,
	IReadOnlyList<MediaId> PulledRemoves,
	IReadOnlyList<MediaId> QueuedAdds,
	IReadOnlyList<MediaId> QueuedRemoves,
	IReadOnlyList<MediaId> Media,
	string? FeedId,
	int? Added,
	int? Removed,
	string? Reason,
	MediaProvider? Provider,
	TimeSpan? RetryAfter
)
{
	public IEnumerable<MediaId> AllMediaIds => PulledAdds.Concat(PulledRemoves).Concat(QueuedAdds).Concat(QueuedRemoves).Concat(Media);
}
