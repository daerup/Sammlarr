using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;

namespace Domain.Notifications.Events;

public abstract record BaseNotification(NotificationEventType Type, UserId UserId, DateTimeOffset OccurredAt) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public abstract string Summary { get; }
}

public sealed record WatchlistSyncedEvent(
	UserId UserId,
	DateTimeOffset OccurredAt,
	IReadOnlyList<MediaId> PulledAdds,
	IReadOnlyList<MediaId> PulledRemoves,
	IReadOnlyList<MediaId> PushedAdds,
	IReadOnlyList<MediaId> PushedRemoves,
	IReadOnlyList<MediaId> FailedPushes
) : BaseNotification(NotificationEventType.WatchlistSynced, UserId, OccurredAt)
{
	public override string Summary =>
		FailedPushes.Count == 0
			? $"Pulled {PulledAdds.Count + PulledRemoves.Count}, pushed {PushedAdds.Count + PushedRemoves.Count}."
			: $"Pulled {PulledAdds.Count + PulledRemoves.Count}, pushed {PushedAdds.Count + PushedRemoves.Count}, {FailedPushes.Count} failed.";
}

public sealed record WatchlistSyncFailedEvent(UserId UserId, DateTimeOffset OccurredAt, string Reason)
	: BaseNotification(NotificationEventType.WatchlistSyncFailed, UserId, OccurredAt)
{
	public override string Summary => Reason;
}

public sealed record TestPingEvent(UserId UserId, DateTimeOffset OccurredAt, Guid TargetId)
	: BaseNotification(NotificationEventType.TestPing, UserId, OccurredAt)
{
	public override string Summary => "This is a test notification from Sammlarr.";
}

public sealed record FeedRunCompletedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, int Added, int Removed)
	: BaseNotification(NotificationEventType.FeedRunCompleted, UserId, OccurredAt)
{
	public override string Summary => $"Added {Added}, removed {Removed}.";
}

public sealed record FeedRunFailedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, string Reason)
	: BaseNotification(NotificationEventType.FeedRunFailed, UserId, OccurredAt)
{
	public override string Summary => Reason;
}

public sealed record ProviderRateLimitedEvent(UserId UserId, DateTimeOffset OccurredAt, MediaProvider Provider, TimeSpan RetryAfter)
	: BaseNotification(NotificationEventType.ProviderRateLimited, UserId, OccurredAt)
{
	public override string Summary => $"Waiting {(int)Math.Ceiling(RetryAfter.TotalSeconds)}s before retrying.";
}
