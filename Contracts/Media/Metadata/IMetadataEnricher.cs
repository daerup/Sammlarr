using Domain.Media;

namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<Result<MediaLookup>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
