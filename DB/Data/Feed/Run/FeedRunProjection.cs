using System.Linq.Expressions;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;

namespace DB.Data.Feed.Run;

internal static class FeedRunProjection
{
	public static readonly Expression<Func<FeedRunRow, FeedRun>> Project = row => new FeedRun
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
			UnmatchedNotFound = row.UnmatchedNotFound,
			UnmatchedCouldNotLookUp = row.UnmatchedCouldNotLookUp,
			Removed = row.Removed,
		},
		Items = row.Items.Select(item => new FeedRunItem { MediaId = MediaId.From(item.MediaId), State = item.State }).ToList(),
		SourceFailure = Optional.Of(row.SourceFailure),
		Calls = row.Calls.Select(call => new FeedRunCalls(call.Service, call.Outcome, call.Calls)).ToList(),
		Times = row.Times.Select(time => new FeedRunTimes(time.Service, time.UpToMs, time.Attempts)).ToList(),
	};

	public static readonly Expression<Func<FeedRunRow, FeedRun>> ProjectWithoutItems = row => new FeedRun
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
			UnmatchedNotFound = row.UnmatchedNotFound,
			UnmatchedCouldNotLookUp = row.UnmatchedCouldNotLookUp,
			Removed = row.Removed,
		},
		Items = new List<FeedRunItem>(),
		SourceFailure = Optional.Of(row.SourceFailure),
		Calls = row.Calls.Select(call => new FeedRunCalls(call.Service, call.Outcome, call.Calls)).ToList(),
		Times = row.Times.Select(time => new FeedRunTimes(time.Service, time.UpToMs, time.Attempts)).ToList(),
	};

	private static readonly Func<FeedRunRow, FeedRun> map = Project.Compile();

	public static FeedRun ToDomain(this FeedRunRow row) => map(row);
}
