using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyConfigRepository
{
	Task<Result<Secret>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task StoreAsync(ApiKeyProvider provider, Secret value, CancellationToken cancellationToken = default);

	Task ClearAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);
}
