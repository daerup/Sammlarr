using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherOption(MediaProvider Provider, IExternalMediaId Id, MediaField Fills, ProviderLoad Load);

internal static class EnricherChoice
{
	internal static Optional<EnricherOption> Next(MediaField needed, IEnumerable<EnricherOption> options, DateTimeOffset now)
	{
		return options
			.OrderByDescending(option => (needed & option.Fills).Count)
			.ThenBy(option => option.Load.Busy)
			.FirstOrNone(option => needed.Overlaps(option.Fills) && option.Load.IsOpen(now));
	}

	internal static MediaField BlockedFills(MediaField needed, IEnumerable<EnricherOption> options, DateTimeOffset now) =>
		options.Where(option => needed.Overlaps(option.Fills) && !option.Load.IsOpen(now)).Aggregate(MediaField.None, (fields, option) => fields | option.Fills);
}
