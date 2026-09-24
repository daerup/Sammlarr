using Domain.Feed.Run;

namespace DB.Data.Feed.Run;

internal record FeedRunItemRow : Entity<Guid>, IUserOwned
{
	public Guid FeedRunId { get; set; }

	public Guid MediaId { get; set; }

	public FeedRunItemState State { get; set; }
}
