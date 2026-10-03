using System.Linq.Expressions;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;

namespace DB.Data.Feed.Run;

internal static class FeedRunProjection
{
	public static readonly Expression<Func<FeedRunRow, Domain.Feed.Run.FeedRun>> Project = row => new Domain.Feed.Run.FeedRun
	{
		Id = RunId.From(row.Id),
		FeedId = FeedId.From(row.FeedId),
		StartedAt = row.StartedAt,
		Duration = TimeSpan.FromMilliseconds(row.DurationMs),
		Strategy = row.Strategy,
		Trigger = row.Trigger,
		Conclusiveness = row.Conclusiveness,
		Counts = new FeedRunCounts
		{
			Total = row.Total,
			Added = row.Added,
			Kept = row.Kept,
			Bypassed = row.Bypassed,
			Filtered = row.Filtered,
			Skipped = row.Skipped,
			Unmatched = row.Unmatched,
			Removed = row.Removed,
		},
		Items = row.Items.Select(item => new FeedRunItem { MediaId = MediaId.From(item.MediaId), State = item.State }).ToList(),
		SourceFailure = Optional.Of(row.SourceFailure),
	};

	private static readonly Func<FeedRunRow, Domain.Feed.Run.FeedRun> map = Project.Compile();

	public static Domain.Feed.Run.FeedRun ToDomain(this FeedRunRow row) => map(row);
}
