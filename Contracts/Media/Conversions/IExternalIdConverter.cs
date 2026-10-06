using Domain.Media.Conversions;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	IReadOnlyList<IdLookup> Lookups { get; }

	Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default);
}
