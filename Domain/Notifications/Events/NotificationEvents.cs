using Domain.Feed;
using Domain.Identity;
using Domain.Media;

namespace Domain.Notifications.Events;

public abstract record BaseNotification(string Type, UserId UserId, DateTimeOffset OccurredAt)
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
) : BaseNotification("watchlist.synced", UserId, OccurredAt);

public sealed record WatchlistItemsAddedEvent(UserId UserId, DateTimeOffset OccurredAt, IReadOnlyList<MediaId> MediaIds)
	: BaseNotification("watchlist.items.added", UserId, OccurredAt);

public sealed record WatchlistItemsRemovedEvent(UserId UserId, DateTimeOffset OccurredAt, IReadOnlyList<MediaId> MediaIds)
	: BaseNotification("watchlist.items.removed", UserId, OccurredAt);

public sealed record TestPingEvent(UserId UserId, DateTimeOffset OccurredAt, Guid TargetId) : BaseNotification("test.ping", UserId, OccurredAt);

public sealed record FeedItemsAddedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, IReadOnlyList<MediaId> MediaIds)
	: BaseNotification("feed.items.added", UserId, OccurredAt);

public sealed record FeedItemsRemovedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, IReadOnlyList<MediaId> MediaIds)
	: BaseNotification("feed.items.removed", UserId, OccurredAt);

public sealed record FeedRunCompletedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, int Added, int Removed)
	: BaseNotification("feed.run.completed", UserId, OccurredAt);

public sealed record FeedRunFailedEvent(UserId UserId, DateTimeOffset OccurredAt, FeedId FeedId, string Reason)
	: BaseNotification("feed.run.failed", UserId, OccurredAt);
