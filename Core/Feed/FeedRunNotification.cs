using Domain.Announcing;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Notifications.Events;

namespace Core.Feed;

internal static class FeedRunNotification
{
	internal static INotification? For(UserId owner, DateTimeOffset now, FeedId id, FeedRunResult result) =>
		result switch
		{
			FeedRunCompleted completed => new FeedRunCompletedEvent(owner, now, id, completed.Delta.Adds.Count, completed.Delta.Drops.Count),
			FeedRunFailed failed => new FeedRunFailedEvent(owner, now, id, failed.ToString()),
			FeedRunAlreadyRunning => null,
		};
}
