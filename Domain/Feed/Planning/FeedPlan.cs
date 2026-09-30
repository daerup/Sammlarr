using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public interface IFeedPlan
{
	IReadOnlyList<QueryVerdict<MediaLookup>> Verdicts { get; }
	IReadOnlyList<UnmatchedMedia> Unresolved { get; }
	FeedDelta Delta { get; }
	Conclusiveness Conclusiveness { get; }
	string? SourceFailure { get; }
}

public sealed record AppendPlan(
	IReadOnlyList<QueryVerdict<MediaLookup>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unresolved,
	FeedDelta Delta,
	Conclusiveness Conclusiveness
) : IFeedPlan
{
	public string? SourceFailure { get; init; }
}

public sealed record MirrorPlan(
	IReadOnlyList<QueryVerdict<MediaLookup>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unresolved,
	FeedDelta Delta,
	Conclusiveness Conclusiveness,
	IReadOnlyDictionary<MediaId, int> MissedRuns
) : IFeedPlan
{
	public string? SourceFailure { get; init; }
}

public static class FeedPlans
{
	public static IFeedPlan Compute(ClassifiedFeedLoad load, IReadOnlySet<MediaId> owned, IReadOnlyDictionary<MediaId, int> absences, FeedStrategy strategy)
	{
		var verdicts = load.Verdicts;
		var qualifying = verdicts.Where(verdict => verdict.Outcome is QueryOutcome.Qualified).Select(verdict => verdict.Item.Identity.Id).ToHashSet();
		var missing = owned.Where(id => !qualifying.Contains(id)).ToList();
		var missedRuns = missing.ToDictionary(id => id, id => absences.GetValueOrDefault(id) + 1);

		var conclusiveness = Assess(qualifying.Count, verdicts.Count, load.Unresolved.Count, load.Completeness);
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
			Append => new AppendPlan(verdicts, load.Unresolved, delta, conclusiveness) { SourceFailure = load.SourceFailure },
			Mirror => new MirrorPlan(verdicts, load.Unresolved, delta, conclusiveness, missedRuns) { SourceFailure = load.SourceFailure },
		};
	}

	private static Conclusiveness Assess(int qualifying, int resolved, int unresolved, Completeness completeness) =>
		completeness switch
		{
			Completeness.Partial => Conclusiveness.StillLoading,
			Completeness.Complete => Settled(qualifying, resolved, unresolved),
		};

	private static Conclusiveness Settled(int qualifying, int resolved, int unresolved)
	{
		if (resolved == 0)
		{
			return unresolved == 0 ? Conclusiveness.NoItems : Conclusiveness.NoMatches;
		}

		return qualifying == 0 ? Conclusiveness.NoneAdmitted : Conclusiveness.Conclusive;
	}
}
