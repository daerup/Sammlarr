using Contracts.Media;

namespace Contracts.Auth;

public interface IApiKeyConfigRepository
{
	ValueTask<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default);

	Task SetApiKeyAsync(MediaProvider provider, string apiKey, CancellationToken cancellationToken = default);

	Task RemoveApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default);
}
