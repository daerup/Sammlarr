using Domain.Providers;
using Domain.Usage;

namespace Domain.Feed.Run;

public sealed record FeedRunCalls(RemoteService Service, UsageOutcome Outcome, long Calls);
