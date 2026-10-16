using Domain.Media;
using Domain.Notifications;
using Domain.Providers;

namespace Messaging.Notifications;

internal sealed record NotificationPayload(
	Guid Id,
	NotificationEventType Type,
	NotificationLevel DefaultLevel,
	DateTimeOffset OccurredAt,
	string Title,
	string Summary,
	IReadOnlyList<MediaId> PulledAdds,
	IReadOnlyList<MediaId> PulledRemoves,
	IReadOnlyList<MediaId> PushedAdds,
	IReadOnlyList<MediaId> PushedRemoves,
	IReadOnlyList<MediaId> Media,
	string? FeedId,
	int? Added,
	int? Removed,
	string? Reason,
	RemoteService? Provider,
	TimeSpan? RetryAfter
)
{
	public IEnumerable<MediaId> AllMediaIds => PulledAdds.Concat(PulledRemoves).Concat(PushedAdds).Concat(PushedRemoves).Concat(Media);
}
