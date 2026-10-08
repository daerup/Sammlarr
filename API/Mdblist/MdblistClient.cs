using API.Http;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Shared.Options;

namespace API.Mdblist;

internal class MdblistClient(HttpClient client, MdblistOptions options, IApiKeyService apiKeys, MdblistBudget budget, ICurrentUser currentUser)
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
		request.StampTriggeredBy(currentUser);
		request.Content = content;
		try
		{
			var response = await client.SendAsync(request, cancellationToken).OnUnauthorizedAsync(() => apiKeys.ReportRejected(ApiKeyProvider.Mdblist));
			budget.Record(apiKey.Value, response.Headers);
			return response;
		}
		catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
		{
			throw new RemoteUnavailableException(RemoteService.Mdblist, ex);
		}
	}
}
