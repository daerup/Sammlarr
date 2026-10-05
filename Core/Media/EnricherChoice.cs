using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherOption(MediaProvider Provider, IExternalMediaId Id, MediaFields Fills, ProviderLoad Load);

internal static class EnricherChoice
{
	internal static Optional<EnricherOption> Next(MediaFields needed, IEnumerable<EnricherOption> options, DateTimeOffset now)
	{
		return options
			.OrderByDescending(option => (needed & option.Fills).Count)
			.ThenBy(option => option.Load.Busy)
			.FirstOrNone(option => needed.Overlaps(option.Fills) && option.Load.IsOpen(now));
	}

	internal static MediaFields BlockedFills(MediaFields needed, IEnumerable<EnricherOption> options, DateTimeOffset now)
	{
		return options
			.Where(option => needed.Overlaps(option.Fills) && !option.Load.IsOpen(now))
			.Aggregate(MediaFields.None, (fields, option) => fields | option.Fills);
	}
}
