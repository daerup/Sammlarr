using Domain.Providers;

namespace Contracts.Auth;

public interface ICredentialRepository
{
	Task<Result<StoredCredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default);

	Task StoreAsync(AuthProvider provider, string accessToken, DateTimeOffset accessTokenExpiresAt, Guid clientId, CancellationToken cancellationToken = default);

	Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default);
}
