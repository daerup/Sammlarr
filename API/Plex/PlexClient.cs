using Contracts.Auth;

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
		await SetAuthHeaders(cancellationToken);

		var request = new HttpRequestMessage(method, uri);

		customHeaders.ForEach(header => request.Headers.Add(header.Item1, header.Item2));
		request.Content = content;
		return await client.SendAsync(request, cancellationToken);
	}

	private async Task SetAuthHeaders(CancellationToken cancellationToken)
	{
		const string plexClientIdentifier = "X-Plex-Client-Identifier";
		const string plexToken = "X-Plex-Token";

		if (client.DefaultRequestHeaders.Contains(plexToken) && client.DefaultRequestHeaders.Contains(plexClientIdentifier))
		{
			return;
		}

		if (await credentialStore.GetAsync(WatchlistProvider.Plex, cancellationToken) is not ICredential credential)
		{
			throw new InvalidOperationException("No valid Plex credentials are stored. Use Plex auth to sign in first.");
		}
		client.DefaultRequestHeaders.Add(plexToken, credential.AccessToken);
		client.DefaultRequestHeaders.Add(plexClientIdentifier, credential.ClientId.ToString());
	}
}
