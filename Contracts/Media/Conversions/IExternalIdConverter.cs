using Contracts;
using Domain.Media;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	);
}
