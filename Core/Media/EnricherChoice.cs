using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct EnricherOption(MediaProvider Provider, IExternalMediaId Id, MediaField Fills, ProviderLoad Load);

internal static class EnricherChoice
{
	internal static EnricherOption? Next(MediaField needed, IEnumerable<EnricherOption> options, DateTimeOffset now) =>
		options
			.Where(option => needed.Overlaps(option.Fills) && option.Load.IsOpen(now))
			.OrderByDescending(option => (needed & option.Fills).Count)
			.ThenBy(option => option.Load.Busy)
			.Select(option => (EnricherOption?)option)
			.FirstOrDefault();

	internal static MediaField Blocked(MediaField needed, IEnumerable<EnricherOption> options, DateTimeOffset now) =>
		options.Where(option => needed.Overlaps(option.Fills) && !option.Load.IsOpen(now)).Aggregate(MediaField.None, (fields, option) => fields | option.Fills);
}
