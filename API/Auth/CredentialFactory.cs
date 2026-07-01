using API.Plex;
using Contracts;
using Contracts.Auth;

namespace API.Auth;

internal sealed class CredentialFactory(IHttpClientFactory httpClientFactory) : ICredentialFactory
{
	public Task<Result<ICredential>> CreateAsync(
		WatchlistProvider provider,
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken = default
	) =>
		provider switch
		{
			WatchlistProvider.Plex => PlexCredential.CreateAsync(clientId, accessToken, expiresAt, httpClientFactory, cancellationToken),
		};
}
