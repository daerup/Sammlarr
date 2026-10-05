using Domain.Media;
using Domain.Providers;

namespace Contracts.Media;

public interface IDetailsEnricher
{
	MediaFields Fills { get; }

	IReadOnlyList<MediaProvider> Accepts { get; }

	Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
