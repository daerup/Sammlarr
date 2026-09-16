using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;

namespace Domain.Notifications.Events;

public abstract record BaseNotification(NotificationEventType Type, UserId UserId, DateTimeOffset OccurredAt)
{
	public Guid Id { get; init; } = Guid.CreateVersion7();
}

public sealed record WatchlistSyncedEvent(
	UserId UserId,
	DateTimeOffset OccurredAt,
	IReadOnlyList<MediaId> PulledAdds,
	IReadOnlyList<MediaId> PulledRemoves,
	IReadOnlyList<MediaId> PushedAdds,
	IReadOnlyList<MediaId> PushedRemoves
) : BaseNotification(NotificationEventType.WatchlistSynced, UserId, OccurredAt);

public sealed record TestPingEvent(UserId UserId, DateTimeOffset OccurredAt, Guid TargetId)
	: BaseNotification(NotificationEventType.TestPing, UserId, OccurredAt);

public sealed record FeedRunCompletedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, int Added, int Removed)
	: BaseNotification(NotificationEventType.FeedRunCompleted, UserId, OccurredAt);

public sealed record FeedRunFailedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, string Reason)
	: BaseNotification(NotificationEventType.FeedRunFailed, UserId, OccurredAt);

public sealed record ProviderRateLimitedEvent(UserId UserId, DateTimeOffset OccurredAt, MediaProvider Provider, TimeSpan RetryAfter)
	: BaseNotification(NotificationEventType.ProviderRateLimited, UserId, OccurredAt);
