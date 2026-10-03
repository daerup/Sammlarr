using Domain.Media;
using Domain.Media.Metadata;

namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	MediaField Fills { get; }

	Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
