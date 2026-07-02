using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyConfigRepository
{
	Task<Result<IApiKey>> GetApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task SetApiKeyAsync(IApiKey apiKey, CancellationToken cancellationToken = default);

	Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);
}
