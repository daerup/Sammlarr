using Domain.Announcing;
using Domain.Identity;

namespace Domain.Feed.Run;

public sealed record FeedRunFinishedEvent(UserId UserId, FeedId FeedId) : IEvent;
