using API.Http;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Providers;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace API.Plex;

internal class PlexClient(HttpClient client, ICredentialService credentials, ICurrentUser currentUser)
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
		var credential = await credentials
			.GetAsync(AuthProvider.Plex, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No valid Plex credentials are stored. Use Plex auth to sign in first."));

		var request = new HttpRequestMessage(method, uri);
		request.Headers.Add("X-Plex-Token", credential.AccessToken);
		request.Headers.Add("X-Plex-Client-Identifier", credential.ClientId.ToString());
		customHeaders.ForEach(header => request.Headers.Add(header.Item1, header.Item2));
		request.Content = content;
		request.StampTriggeredBy(currentUser);

		try
		{
			return await client.SendAsync(request, cancellationToken).OnUnauthorizedAsync(() => credentials.ReportRejected(AuthProvider.Plex));
		}
		catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
		{
			throw new ProviderUnavailableException(MediaProvider.Plex, ex);
		}
	}
}
