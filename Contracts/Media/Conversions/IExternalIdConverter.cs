using Domain.Media.Conversions;
using Domain.Providers;

namespace Contracts.Media.Conversions;

public interface IExternalIdConverter
{
	IReadOnlyList<IdLookup> Lookups { get; }

	Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default);
}
