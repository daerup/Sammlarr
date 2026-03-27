using Contracts.Auth;
using DB.Data.Entities;

namespace DB.Data.Stores;

public interface ICredentialStore
{
	Task<Credential?> GetAsync(AuthProvider authProvider, CancellationToken cancellationToken = default);

	public Task StoreAsync(
		AuthProvider authProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	);

	Task ClearAsync(AuthProvider authProvider, CancellationToken cancellationToken = default);
}
