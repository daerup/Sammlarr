namespace Contracts.Auth;

public interface ICredentialRepository
{
	Task<Result<ICredential>> GetAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default);

	Task StoreAsync(
		WatchlistProvider watchlistProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	);

	Task ClearAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default);
}
