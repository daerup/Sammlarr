using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface ICredentialRepository
{
	ValueTask<Result<ICredential>> GetAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default);

	Task StoreAsync(
		WatchlistProvider watchlistProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	);

	Task ClearAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default);
}
