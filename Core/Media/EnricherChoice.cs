using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherCandidate(MediaProvider Provider, IExternalMediaId Id, MediaFields Fills, ProviderLoad Load)
{
	public bool CanHelp(MediaFields needed, DateTimeOffset now) => needed.Overlaps(Fills) && Load.IsOpen(now);
}

internal static class EnricherChoice
{
	internal static Optional<EnricherCandidate> Next(MediaFields needed, IEnumerable<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		return leftToAsk
			.OrderByDescending(candidate => (needed & candidate.Fills).Count)
			.ThenBy(candidate => candidate.Load.Busy)
			.FirstOrNone(candidate => candidate.CanHelp(needed, now));
	}

	internal static MediaFields BlockedFills(MediaFields needed, IEnumerable<EnricherCandidate> leftToAsk, DateTimeOffset now)
	{
		return leftToAsk
			.Where(candidate => needed.Overlaps(candidate.Fills) && !candidate.Load.IsOpen(now))
			.Aggregate(MediaFields.None, (fields, candidate) => fields | candidate.Fills);
	}
}
