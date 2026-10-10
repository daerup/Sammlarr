using Domain.Feed;

namespace DB.Data.Feed.Run;

internal record FeedRunRow : Entity<Guid>, IUserOwned
{
	public Guid FeedId { get; set; }

	public DateTimeOffset StartedAt { get; set; }

	public long DurationMs { get; set; }

	public FeedStrategy Strategy { get; set; } = new(new Append());

	public FeedRunTrigger Trigger { get; set; } = new(new Manual());

	public Conclusiveness Conclusiveness { get; set; }

	public int Total { get; set; }
	public int Added { get; set; }
	public int Kept { get; set; }
	public int Bypassed { get; set; }
	public int Filtered { get; set; }
	public int Skipped { get; set; }
	public int UnmatchedNotFound { get; set; }
	public int UnmatchedCouldNotLookUp { get; set; }
	public int UnmatchedConflict { get; set; }
	public int Removed { get; set; }
	public int Known { get; set; }
	public int NotFoundEarlier { get; set; }

	public string? SourceFailure { get; set; }

	public List<FeedRunItemRow> Items { get; set; } = [];

	public List<FeedRunCallsRow> Calls { get; set; } = [];

	public List<FeedRunTimesRow> Times { get; set; } = [];
}
