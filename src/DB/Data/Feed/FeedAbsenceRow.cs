namespace DB.Data.Feed;

internal sealed record FeedAbsenceRow : IUserOwned
{
	public Guid FeedId { get; set; }

	public Guid MediaId { get; set; }

	public int MissedRuns { get; set; }
}
