using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<IMediaDetailResult?> LookUpAsync(IMediaId id, CancellationToken cancellationToken = default);
}
