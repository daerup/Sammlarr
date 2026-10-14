using Domain.Providers;
using Domain.Usage;

namespace DB.Data.Feed.Run;

internal sealed record FeedRunCallsRow : Entity<Guid>, IUserOwned
{
	public Guid FeedRunId { get; set; }

	public RemoteService Service { get; set; }

	public UsageOutcome Outcome { get; set; }

	public long Calls { get; set; }
}
