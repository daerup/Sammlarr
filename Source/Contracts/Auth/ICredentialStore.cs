namespace Contracts.Auth;

public interface ICredentialStore
{
	Task<StoredCredential?> GetAsync(AuthProvider authProvider, CancellationToken cancellationToken = default);

	Task StoreAsync(
		AuthProvider authProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	);

	Task ClearAsync(AuthProvider authProvider, CancellationToken cancellationToken = default);
}
