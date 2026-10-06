using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;

namespace Domain.Notifications;

public sealed record WantlistSyncCompletedNotification(
	UserId UserId,
	DateTimeOffset OccurredAt,
	IReadOnlyList<MediaId> PulledAdds,
	IReadOnlyList<MediaId> PulledRemoves,
	IReadOnlyList<MediaId> PushedAdds,
	IReadOnlyList<MediaId> PushedRemoves,
	IReadOnlyList<MediaId> FailedPushes
) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public NotificationEventType Type => NotificationEventType.WantlistSyncCompleted;

	public string Summary =>
		FailedPushes.Count == 0
			? $"Pulled {PulledAdds.Count + PulledRemoves.Count}, pushed {PushedAdds.Count + PushedRemoves.Count}."
			: $"Pulled {PulledAdds.Count + PulledRemoves.Count}, pushed {PushedAdds.Count + PushedRemoves.Count}, {FailedPushes.Count} failed.";

	public NotificationLevel DefaultLevel => FailedPushes.Count == 0 ? NotificationLevel.Success : NotificationLevel.Warning;
}

public sealed record WantlistSyncFailedNotification(UserId UserId, DateTimeOffset OccurredAt, string Reason) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public NotificationEventType Type => NotificationEventType.WantlistSyncFailed;

	public string Summary => Reason;

	public NotificationLevel DefaultLevel => NotificationLevel.Error;
}

public sealed record TestPingNotification(UserId UserId, DateTimeOffset OccurredAt, Guid TargetId) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public NotificationEventType Type => NotificationEventType.TestPing;

	public string Summary => "This is a test notification from Sammlarr.";

	public NotificationLevel DefaultLevel => NotificationLevel.Info;
}

public sealed record FeedRunCompletedNotification(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, int Added, int Removed) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public NotificationEventType Type => NotificationEventType.FeedRunCompleted;

	public string Summary => $"Added {Added}, removed {Removed}.";

	public NotificationLevel DefaultLevel => NotificationLevel.Success;
}

public sealed record FeedRunFailedNotification(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, string Reason) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public NotificationEventType Type => NotificationEventType.FeedRunFailed;

	public string Summary => Reason;

	public NotificationLevel DefaultLevel => NotificationLevel.Error;
}

public sealed record ProviderRateLimitedNotification(UserId UserId, DateTimeOffset OccurredAt, RemoteService Provider, TimeSpan RetryAfter) : INotification
{
	public Guid Id { get; init; } = Guid.CreateVersion7();

	public NotificationEventType Type => NotificationEventType.ProviderRateLimited;

	public string Summary => $"Waiting {(int)Math.Ceiling(RetryAfter.TotalSeconds)}s before retrying.";

	public NotificationLevel DefaultLevel => NotificationLevel.Warning;
}
