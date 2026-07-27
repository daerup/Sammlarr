using API.Http;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;

namespace API.Plex;

internal class PlexClient(HttpClient client, ICredentialRepository credentialStore)
{
	public const string Unauthenticated = "Plex";
	public const int ContainerSize = 100;

	public async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string uri,
		CancellationToken cancellationToken = default,
		HttpContent? content = null,
		params List<(string, string)> customHeaders
	)
	{
		var credential = await credentialStore
			.GetAsync(AuthProvider.Plex, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No valid Plex credentials are stored. Use Plex auth to sign in first."));

		var request = new HttpRequestMessage(method, uri);
		request.Headers.Add("X-Plex-Token", credential.AccessToken);
		request.Headers.Add("X-Plex-Client-Identifier", credential.ClientId.ToString());
		customHeaders.ForEach(header => request.Headers.Add(header.Item1, header.Item2));
		request.Content = content;

		return await client.SendAsync(request, cancellationToken).OnUnauthorizedAsync(() => credentialStore.Invalidate(AuthProvider.Plex));
	}
}
