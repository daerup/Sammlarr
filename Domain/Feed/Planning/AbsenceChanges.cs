using Domain.Media;

namespace Domain.Feed.Planning;

public sealed record AbsenceChanges(IReadOnlySet<MediaId> Missed)
{
	public static Optional<AbsenceChanges> Of(IFeedPlan plan, IReadOnlyCollection<MediaId> removed)
	{
		if (plan is not MirrorPlan { Conclusiveness: Conclusiveness.Conclusive } mirror)
		{
			return new None();
		}

		return Some.Of(new AbsenceChanges(mirror.MissedRuns.Keys.Except(removed).ToHashSet()));
	}
}
