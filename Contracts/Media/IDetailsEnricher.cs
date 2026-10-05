using Domain.Media;

namespace Contracts.Media;

public interface IDetailsEnricher
{
	MediaFields Fills { get; }

	Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
