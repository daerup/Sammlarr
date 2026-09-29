using Domain.Providers;

namespace Contracts.Auth;

public interface IApiKeyService
{
	Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);
}
