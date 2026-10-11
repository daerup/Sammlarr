using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherCandidate(RemoteService Remote, IExternalMediaId Id, MediaFields Fills, RemoteLoad Load, TimeSpan ShownWait)
{
	public bool CanHelp(FieldNeeds needed, DateTimeOffset now)
	{
		return Load.Takes(now, Load.EnrichWait) && (needed.Required.Overlaps(Fills) || (needed.Shown.Overlaps(Fills) && Load.AnswersWithin(now, ShownWait)));
	}
}

internal static class EnricherChoice
{
	internal static Optional<EnricherCandidate> Next(
		FieldNeeds needed,
		IEnumerable<EnricherCandidate> leftToAsk,
		IReadOnlySet<RemoteService> preferred,
		DateTimeOffset now
	)
	{
		return leftToAsk
			.Where(candidate => candidate.CanHelp(needed, now))
			.OrderByDescending(candidate => candidate.Load.IsOpen(now))
			.ThenByDescending(candidate => needed.All.DetailCount > 0 && preferred.Contains(candidate.Remote))
			.ThenByDescending(candidate => (needed.All & candidate.Fills).DetailCount)
			.ThenByDescending(candidate => (needed.All & candidate.Fills).Ratings.Count)
			.ThenBy(candidate => candidate.Load.EstimatedWait)
			.ThenBy(candidate => candidate.Remote)
			.FirstOrNone();
	}

	internal static IEnumerable<EnricherCandidate> Unasked(FieldNeeds needed, IEnumerable<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		return leftToAsk.Where(candidate => needed.All.Overlaps(candidate.Fills) && !candidate.CanHelp(needed, now));
	}

	internal static bool NeedsRefetch(CachedDetails cached, IReadOnlyList<EnricherCandidate> leftToAsk, FieldNeeds needs, DateTimeOffset now, TimeSpan freshFor)
	{
		var gaps = needs.Of(cached.Details.Missing);
		return cached.IsStale(now, freshFor) || leftToAsk.Any(candidate => candidate.CanHelp(gaps, now));
	}
}
