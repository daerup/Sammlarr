using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	MediaProvider Provider { get; }

	IReadOnlyList<IdLookup> Lookups { get; }

	IReadOnlyList<IdRoute> Routes { get; }

	Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IdRoute route,
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	);
}
