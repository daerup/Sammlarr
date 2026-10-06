using API.Http;
using Contracts.Auth;
using Domain.Auth;

namespace API.Plex;

internal sealed class PlexTokenCheck(IHttpClientFactory httpClientFactory) : ICredentialValidator
{
	public async Task<AccessCheck> CheckAsync(Guid clientId, string accessToken, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, PlexEndpoints.UserPath);
		request.Headers.Add("X-Plex-Token", accessToken);
		request.Headers.Add("X-Plex-Client-Identifier", clientId.ToString());
		return await httpClientFactory.CreateClient(PlexClient.Unauthenticated).CheckAccessAsync(request, cancellationToken);
	}
}
