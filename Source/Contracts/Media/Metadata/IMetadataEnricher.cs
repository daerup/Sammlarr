using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<IMediaDetailResult?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
