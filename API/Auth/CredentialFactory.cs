using API.Plex;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;

namespace API.Auth;

internal sealed class CredentialFactory(IHttpClientFactory httpClientFactory) : ICredentialFactory
{
	public Task<Result<ICredential>> CreateAsync(
		AuthProvider provider,
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken = default
	) =>
		provider switch
		{
			AuthProvider.Plex => PlexCredential.CreateAsync(clientId, accessToken, expiresAt, httpClientFactory, cancellationToken),
		};
}
