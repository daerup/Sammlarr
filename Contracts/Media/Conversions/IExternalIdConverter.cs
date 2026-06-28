using Contracts;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	Task<Optional<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	);
}
