using Domain.Media.Conversions;
using Domain.Providers;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	MediaProvider Provider { get; }

	IReadOnlyList<IdLookup> Lookups { get; }

	Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default);
}
