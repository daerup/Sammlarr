using API.Http;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;

namespace API.Mal;

internal sealed class MalClient(HttpClient client, IApiKeyService apiKeys, ICurrentUser currentUser)
{
	public const string Unauthenticated = "MyAnimeList";

	public const string ClientIdHeader = "X-MAL-CLIENT-ID";

	public async Task<HttpResponseMessage> GetAsync(string endpoint, IApiKey clientId, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
		request.Headers.Add(ClientIdHeader, clientId.Value);
		request.StampTriggeredBy(currentUser);
		return await client.SendAsync(request, cancellationToken).OnUnauthorizedAsync(() => apiKeys.ReportRejected(ApiKeyProvider.Mal));
	}
}
