using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Humanizer;

namespace UI.Features.Home.Components;

public sealed record ActivityRun(FeedRun Run, string FeedName)
{
	public bool Gone { get; init; }

	internal bool Inconclusive => Run.Conclusiveness is not (Conclusiveness.Conclusive or Conclusiveness.Pending);

	internal string Outcome =>
		Run.Conclusiveness switch
		{
			Conclusiveness.Conclusive => $"{Run.Counts.Total} seen, {Run.Counts.Kept} kept",
			Conclusiveness.StillLoading => "the list stopped loading partway, so nothing was removed",
			Conclusiveness.NoItems => "the list came back empty",
			Conclusiveness.NoMatches => $"{"title".ToQuantity(Run.Counts.Total)} seen, none matched",
			Conclusiveness.NoneAdmitted => $"{"title".ToQuantity(Run.Counts.Total)} seen, all filtered out",
			Conclusiveness.Pending => "still running",
			Conclusiveness.Unresolved => "some titles couldn't be looked up, so nothing was removed",
			Conclusiveness.Stopped => "the run stopped before it finished",
		};

	internal string? Reason => Run.SourceFailure.Match(failure => failure, UnrecordedReason);

	private string? UnrecordedReason()
	{
		if (!Run.Failed)
		{
			return null;
		}
		return Run.Conclusiveness is Conclusiveness.NoItems ? "The list came back empty." : "The reason wasn't recorded.";
	}
}

public sealed record FeedContribution(FeedId FeedId, string Name, int Added, int Removed, int Runs, RunId LatestRunId, bool Gone);

public sealed record ActivityDay(
	DateOnly Date,
	IReadOnlyList<MediaId> Added,
	IReadOnlyList<MediaId> Removed,
	IReadOnlyList<FeedContribution> Feeds,
	IReadOnlyList<ActivityRun> Runs,
	IReadOnlyList<ActivityRun> Inconclusive,
	int ChangedRuns
)
{
	public int RunCount => Runs.Count;
}

internal static class ActivityLog
{
	internal static IReadOnlyList<ActivityDay> Days(IEnumerable<FeedRun> runs, IReadOnlyDictionary<FeedId, string> feedNames, TimeZoneInfo zone) =>
		[
			.. runs.Select(run => new ActivityRun(run, FeedLabel.Of(run.FeedId, feedNames)) { Gone = !feedNames.ContainsKey(run.FeedId) })
				.GroupBy(entry => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.Run.StartedAt, zone).DateTime))
				.OrderByDescending(group => group.Key)
				.Select(Day)
				.Where(day => day.ChangedRuns > 0 || day.Inconclusive.Count > 0),
		];

	private static ActivityDay Day(IGrouping<DateOnly, ActivityRun> group)
	{
		var ordered = group.OrderByDescending(entry => entry.Run.StartedAt).ToList();
		var changed = ordered.Where(entry => !entry.Inconclusive && entry.Run.HasChanges).ToList();

		return new ActivityDay(
			group.Key,
			[.. changed.SelectMany(entry => Items(entry, FeedRunItemState.Added)).Distinct()],
			[.. changed.SelectMany(entry => Items(entry, FeedRunItemState.Removed)).Distinct()],
			Contributions(changed),
			ordered,
			[.. ordered.Where(entry => entry.Inconclusive)],
			changed.Count
		);
	}

	private static IReadOnlyList<FeedContribution> Contributions(IEnumerable<ActivityRun> changed) =>
		[
			.. changed
				.GroupBy(entry => entry.Run.FeedId)
				.Select(group => new FeedContribution(
					group.Key,
					group.First().FeedName,
					group.Sum(entry => entry.Run.Counts.Added),
					group.Sum(entry => entry.Run.Counts.Removed),
					group.Count(),
					group.First().Run.Id,
					group.First().Gone
				))
				.OrderByDescending(contribution => contribution.Added + contribution.Removed),
		];

	private static IEnumerable<MediaId> Items(ActivityRun entry, FeedRunItemState state) =>
		entry.Run.Items.Where(item => item.State == state).Select(item => item.MediaId);
}
