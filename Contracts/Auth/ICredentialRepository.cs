using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface ICredentialRepository
{
	ValueTask<Result<ICredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default);

	Task StoreAsync(AuthProvider provider, string accessToken, DateTimeOffset accessTokenExpiresAt, Guid clientId, CancellationToken cancellationToken = default);

	Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default);

	void Invalidate(AuthProvider provider);
}
