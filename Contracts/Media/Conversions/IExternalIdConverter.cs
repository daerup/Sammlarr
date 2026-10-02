using Domain.Media;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	IReadOnlyList<IdRoute> Routes { get; }

	Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IdRoute route,
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	);
}
