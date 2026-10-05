using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherCandidate(MediaProvider Provider, IExternalMediaId Id, MediaFields Fills, ProviderLoad Load)
{
	public bool CanHelp(MediaFields needed, DateTimeOffset now) => needed.Overlaps(Fills) && Load.IsOpen(now);
}

internal static class EnricherChoice
{
	private static readonly TimeSpan RefetchMissingCooldown = TimeSpan.FromHours(1);

	internal static Optional<EnricherCandidate> Next(
		MediaFields needed,
		IEnumerable<EnricherCandidate> leftToAsk,
		Optional<MediaProvider> preferred,
		DateTimeOffset now
	)
	{
		return leftToAsk
			.Where(candidate => candidate.CanHelp(needed, now))
			.OrderByDescending(candidate => preferred.When(provider => provider == candidate.Provider).IsSome())
			.ThenByDescending(candidate => (needed & candidate.Fills).DetailCount)
			.ThenBy(candidate => candidate.Load.Busy)
			.ThenBy(candidate => candidate.Provider)
			.FirstOrNone();
	}

	internal static Optional<EnricherCandidate> WorthWaitingFor(
		MediaFields needed,
		IEnumerable<EnricherCandidate> leftToAsk,
		IReadOnlySet<MediaProvider> waited,
		DateTimeOffset now
	)
	{
		return leftToAsk
			.Where(candidate => needed.Overlaps(candidate.Fills) && !waited.Contains(candidate.Provider))
			.Where(candidate => candidate.Load.FreesWithin(now, candidate.Load.EnrichWait))
			.OrderBy(candidate => candidate.Load.FreeAt(now))
			.FirstOrNone();
	}

	internal static bool NeedsRefetch(CachedDetails cached, IReadOnlyList<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		var stale = cached.IsStale(now);
		var pastCooldown = now - cached.AskedAt >= RefetchMissingCooldown;
		var someoneNewCanHelp = leftToAsk.Any(candidate => candidate.CanHelp(cached.Details.Missing, now));
		return stale || (pastCooldown && someoneNewCanHelp);
	}
}
