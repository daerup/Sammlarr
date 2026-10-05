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

	internal static Optional<EnricherCandidate> Next(MediaFields needed, IEnumerable<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		return leftToAsk
			.OrderByDescending(candidate => (needed & candidate.Fills).Count)
			.ThenBy(candidate => candidate.Load.Busy)
			.FirstOrNone(candidate => candidate.CanHelp(needed, now));
	}

	internal static bool NeedsRefetch(CachedDetails cached, IReadOnlyList<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		var stale = cached.IsStale(now);
		var pastCooldown = now - cached.AskedAt >= RefetchMissingCooldown;
		var someoneNewCanHelp = leftToAsk.Any(candidate => candidate.CanHelp(cached.Details.Missing, now));
		return stale || (pastCooldown && someoneNewCanHelp);
	}
}
