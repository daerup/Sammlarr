using Domain.Feed;
using Domain.Identity;

namespace Core.Feed.Scheduling;

internal sealed record ScheduledFeed(FeedId FeedId, UserId OwnerId, FeedRunTrigger Trigger);
