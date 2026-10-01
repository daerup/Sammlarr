using API.Http;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;

namespace API.Tmdb;

internal class TmdbClient(HttpClient client, TmdbOptions options, IApiKeyService apiKeys, ICurrentUser currentUser)
{
	public const string Unauthenticated = "Tmdb";
	public const string QueryParam = "api_key";

	public async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		IApiKey apiKey,
		CancellationToken cancellationToken = default,
		HttpContent? content = null
	)
	{
		using var request = new HttpRequestMessage(method, ApiKeyRequest.BuildUri(options.BaseUrl, endpoint, QueryParam, apiKey.Value));
		request.StampTriggeredBy(currentUser);
		request.Content = content;
		return await client.SendAsync(request, cancellationToken).OnUnauthorizedAsync(() => apiKeys.ReportRejected(ApiKeyProvider.Tmdb));
	}
}
