using Domain.Providers;

namespace DB.Data.Feed.Run;

internal record FeedRunTimesRow : Entity<Guid>, IUserOwned
{
	public Guid FeedRunId { get; set; }

	public RemoteService Service { get; set; }

	public int UpToMs { get; set; }

	public long Attempts { get; set; }
}
