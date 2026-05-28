namespace Contracts.Auth;

public interface ICredentialStore
{
	ValueTask<StoredCredential?> GetAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default);

	Task StoreAsync(
		WatchlistProvider watchlistProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	);

	Task ClearAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default);
}
