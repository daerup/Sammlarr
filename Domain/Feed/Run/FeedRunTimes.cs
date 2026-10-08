using Domain.Providers;

namespace Domain.Feed.Run;

public sealed record FeedRunTimes(RemoteService Service, int UpToMs, long Attempts);
