using API.Http;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;

namespace API.Mdblist;

internal class MdblistClient(HttpClient client, MdblistOptions options, IApiKeyConfigRepository apiKeys)
{
	public const string Unauthenticated = "Mdblist";
	public const string QueryParam = "apikey";

	public async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		IApiKey apiKey,
		CancellationToken cancellationToken = default,
		HttpContent? content = null
	)
	{
		using var request = new HttpRequestMessage(method, ApiKeyRequest.BuildUri(options.BaseUrl, endpoint, QueryParam, apiKey.Value));
		request.Content = content;
		return await client.SendAsync(request, cancellationToken).OnUnauthorizedAsync(() => apiKeys.Invalidate(ApiKeyProvider.Mdblist));
	}
}
