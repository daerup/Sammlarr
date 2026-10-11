using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherCandidate(RemoteService Remote, IExternalMediaId Id, MediaFields Fills, RemoteLoad Load)
{
	public bool CanHelp(MediaFields needed, DateTimeOffset now) => needed.Overlaps(Fills) && Load.Takes(now, Load.EnrichWait);
}

internal static class EnricherChoice
{
	private static readonly TimeSpan RefetchMissingCooldown = TimeSpan.FromHours(1);

	internal static Optional<EnricherCandidate> Next(
		MediaFields needed,
		IEnumerable<EnricherCandidate> leftToAsk,
		IReadOnlySet<RemoteService> preferred,
		DateTimeOffset now
	)
	{
		return leftToAsk
			.Where(candidate => candidate.CanHelp(needed, now))
			.OrderByDescending(candidate => candidate.Load.IsOpen(now))
			.ThenByDescending(candidate => preferred.Contains(candidate.Remote))
			.ThenByDescending(candidate => (needed & candidate.Fills).DetailCount)
			.ThenBy(candidate => candidate.Load.EstimatedWait)
			.ThenBy(candidate => candidate.Remote)
			.FirstOrNone();
	}

	internal static Optional<EnricherCandidate> Blocked(MediaFields needed, IEnumerable<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		return leftToAsk.Where(candidate => needed.Overlaps(candidate.Fills) && !candidate.Load.Takes(now, candidate.Load.EnrichWait)).FirstOrNone();
	}

	internal static bool NeedsRefetch(CachedDetails cached, IReadOnlyList<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		var stale = cached.IsStale(now);
		var pastCooldown = now - cached.AskedAt >= RefetchMissingCooldown;
		var someoneNewCanHelp = leftToAsk.Any(candidate => candidate.CanHelp(cached.Details.Missing, now));
		return stale || (pastCooldown && someoneNewCanHelp);
	}
}
