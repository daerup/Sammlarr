using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public interface IFeedPlan
{
	IReadOnlyList<QueryVerdict<MatchedMedia>> Verdicts { get; }
	IReadOnlyList<UnmatchedMedia> Unmatched { get; }
	FeedDelta Delta { get; }
	Conclusiveness Conclusiveness { get; }
	Optional<string> SourceFailure { get; }
}

public sealed record AppendPlan(
	IReadOnlyList<QueryVerdict<MatchedMedia>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unmatched,
	FeedDelta Delta,
	Conclusiveness Conclusiveness
) : IFeedPlan
{
	public Optional<string> SourceFailure { get; init; } = new None();
}

public sealed record MirrorPlan(
	IReadOnlyList<QueryVerdict<MatchedMedia>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unmatched,
	FeedDelta Delta,
	Conclusiveness Conclusiveness,
	IReadOnlyDictionary<MediaId, int> MissedRuns
) : IFeedPlan
{
	public Optional<string> SourceFailure { get; init; } = new None();
}

public static class FeedPlans
{
	public static IFeedPlan Compute(ClassifiedFeedLoad load, IReadOnlySet<MediaId> owned, IReadOnlyDictionary<MediaId, int> absences, FeedStrategy strategy)
	{
		var verdicts = load.Verdicts;
		var qualifying = verdicts.Where(verdict => verdict.Outcome is QueryOutcome.Qualified).Select(verdict => verdict.Item.Identity.Id).ToHashSet();
		var missing = owned.Where(id => !qualifying.Contains(id)).ToList();
		var missedRuns = missing.ToDictionary(id => id, id => absences.GetValueOrDefault(id) + 1);

		var conclusiveness = Assess(qualifying.Count, verdicts.Count, load.Unmatched.Count, load.State);
		var conclusive = conclusiveness is Conclusiveness.Conclusive;

		var delta = new FeedDelta(
			Adds:
			[
				.. verdicts.Where(verdict => verdict.WillImport && !owned.Contains(verdict.Item.Identity.Id)).Select(verdict => verdict.Item.Identity.Id).Distinct(),
			],
			Keeps: [.. owned.Where(qualifying.Contains)],
			Drops: strategy is Mirror mirror && conclusive ? [.. missing.Where(id => missedRuns[id] >= mirror.AbsenceThreshold)] : []
		);

		return strategy switch
		{
			Append => new AppendPlan(verdicts, load.Unmatched, delta, conclusiveness) { SourceFailure = load.SourceFailure },
			Mirror => new MirrorPlan(verdicts, load.Unmatched, delta, conclusiveness, missedRuns) { SourceFailure = load.SourceFailure },
		};
	}

	private static Conclusiveness Assess(int qualifying, int resolved, int unmatched, FeedLoadState state)
	{
		return state switch
		{
			FeedLoadState.Unfinished => Conclusiveness.StillLoading,
			FeedLoadState.Finished => Settled(qualifying, resolved, unmatched),
		};
	}

	private static Conclusiveness Settled(int qualifying, int resolved, int unmatched)
	{
		if (resolved == 0)
		{
			return unmatched == 0 ? Conclusiveness.NoItems : Conclusiveness.NoMatches;
		}

		return qualifying == 0 ? Conclusiveness.NoneAdmitted : Conclusiveness.Conclusive;
	}
}
