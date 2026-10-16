using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyConfigRepository
{
	Task<Result<string>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task StoreAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default);

	Task ClearAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);
}
