using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyConfigRepository
{
	Task<Result<string>> GetApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task SetApiKeyAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default);

	Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);
}
