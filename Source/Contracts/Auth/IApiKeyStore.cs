using Contracts.Media;

namespace Contracts.Auth;

public interface IApiKeyStore
{
	ValueTask<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default);

	Task SetApiKeyAsync(MediaProvider provider, string apiKey, CancellationToken cancellationToken = default);

	Task ClearApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default);
}
