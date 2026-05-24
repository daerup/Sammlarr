using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<IMediaDetail?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
