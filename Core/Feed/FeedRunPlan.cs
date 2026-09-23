using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;

namespace Core.Feed;

internal sealed record FeedRunPlan(FeedDelta Delta, Conclusiveness Conclusiveness, IReadOnlyDictionary<MediaId, int> RunsRemaining)
{
	internal static FeedRunPlan Compute(
		IReadOnlyList<QueryVerdict<MediaLookup>> verdicts,
		int unresolvedCount,
		IReadOnlySet<MediaId> owned,
		IReadOnlyDictionary<MediaId, int> absences,
		FeedStrategy strategy,
		Completeness source
	)
	{
		var qualifying = verdicts.Where(verdict => verdict.Outcome is QueryOutcome.Qualified).Select(verdict => verdict.Item.Identity.Id).ToHashSet();
		var missing = owned.Where(id => !qualifying.Contains(id)).ToList();
		var threshold = strategy is Mirror mirror ? mirror.AbsenceThreshold : 0;

		var runsRemaining = missing.ToDictionary(id => id, id => Math.Max(0, threshold - (absences.GetValueOrDefault(id) + 1)));

		var delta = new FeedDelta(
			Adds:
			[
				.. verdicts.Where(verdict => verdict.WillImport && !owned.Contains(verdict.Item.Identity.Id)).Select(verdict => verdict.Item.Identity.Id).Distinct(),
			],
			Keeps: [.. owned.Where(qualifying.Contains)],
			Drops: strategy is Mirror ? [.. missing.Where(id => runsRemaining[id] == 0)] : []
		);

		var conclusiveness = Assess(qualifying.Count, verdicts.Count, unresolvedCount, source);
		var conclusive = conclusiveness is Conclusiveness.Conclusive;

		return new FeedRunPlan(conclusive ? delta : delta with { Drops = [] }, conclusiveness, runsRemaining);
	}

	private static Conclusiveness Assess(int qualifying, int resolved, int unresolved, Completeness source) =>
		source switch
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
