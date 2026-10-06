using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyService
{
	ValueTask<Result<IApiKey>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task<bool> IsStoredAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task<Result<IApiKey>> TestAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default);

	Task StoreAsync(IApiKey key, CancellationToken cancellationToken = default);

	Task ClearAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	void ReportRejected(ApiKeyProvider provider);
}
