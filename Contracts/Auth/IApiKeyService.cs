using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyService
{
	ValueTask<Result<IApiKey>> GetApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task<bool> HasApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task<IReadOnlySet<FeedProvider>> GetFeedProvidersWithoutKeyAsync(CancellationToken cancellationToken = default);

	Task<Result<IApiKey>> TestApiKeyAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default);

	Task SaveApiKeyAsync(IApiKey apiKey, CancellationToken cancellationToken = default);

	Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	void Invalidate(ApiKeyProvider provider);
}
