using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;

namespace Core.Feed;

internal sealed record FeedRunPlan(FeedDelta Delta, Conclusiveness Conclusiveness)
{
	internal static FeedRunPlan Compute(
		IReadOnlyList<QueryVerdict<MediaLookup>> verdicts,
		int unresolvedCount,
		FeedOwnership owned,
		FeedStrategy strategy,
		FeedSource source
	)
	{
		var qualifying = verdicts.Where(verdict => verdict.Outcome is QueryOutcome.Qualified).Select(verdict => verdict.Item.Identity.Id).ToHashSet();

		var delta = new FeedDelta(
			Adds: [.. verdicts.Where(verdict => verdict.WillImport && !owned.Owns(verdict.Item.Identity.Id)).Select(verdict => verdict.Item.Identity.Id).Distinct()],
			Keeps: [.. owned.Active.Where(qualifying.Contains)],
			Drops: strategy is Mirror ? [.. owned.Active.Where(id => !qualifying.Contains(id))] : [],
			Waives: [.. owned.Waived]
		);

		var conclusiveness = Assess(qualifying.Count, verdicts.Count, unresolvedCount, source);
		var conclusive = conclusiveness is Conclusiveness.Conclusive;

		return new FeedRunPlan(conclusive ? delta : delta with { Drops = [] }, conclusiveness);
	}

	private static Conclusiveness Assess(int qualifying, int resolved, int unresolved, FeedSource source) =>
		source switch
		{
			FeedSource.Partial => Conclusiveness.StillLoading,
			FeedSource.Complete => Settled(qualifying, resolved, unresolved),
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
