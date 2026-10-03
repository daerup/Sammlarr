using Domain.Announcing;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Notifications;

namespace Core.Feed;

internal static class FeedRunNotification
{
	internal static Optional<INotification> For(UserId owner, DateTimeOffset now, FeedId id, FeedRunResult result)
	{
		return result switch
		{
			FeedRunCompleted completed => Some.Of<INotification>(
				new FeedRunCompletedNotification(owner, now, id, completed.Delta.Adds.Count, completed.Delta.Drops.Count)
			),
			FeedRunFailed failed => Some.Of<INotification>(new FeedRunFailedNotification(owner, now, id, failed.ToString())),
			FeedRunAlreadyRunning => new None(),
		};
	}
}
