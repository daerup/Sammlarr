using Domain.Media;

namespace Contracts.Media;

public interface IDetailsEnricher
{
	MediaField Fills { get; }

	Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
