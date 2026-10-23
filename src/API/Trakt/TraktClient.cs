using System.Net;
using API.Http;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;

namespace API.Trakt;

internal sealed class TraktClient(HttpClient client, IApiKeyService apiKeys, ICurrentUser currentUser)
{
	public const string Unauthenticated = "Trakt";

	public async Task<HttpResponseMessage> GetAsync(string endpoint, IApiKey clientId, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
		Sign(request, clientId.Value.Reveal());
		request.StampTriggeredBy(currentUser);
		var response = await client.SendAsync(request, cancellationToken);
		if (IsRejected(response))
		{
			apiKeys.ReportRejected(ApiKeyProvider.Trakt);
		}

		return response;
	}

	internal static void Sign(HttpRequestMessage request, string clientId)
	{
		request.Headers.Add("trakt-api-key", clientId);
		request.Headers.Add("trakt-api-version", "2");
	}

	internal static bool IsBlocked(HttpResponseMessage response)
	{
		return response.StatusCode is HttpStatusCode.Forbidden && response.Content.Headers.ContentType?.MediaType is "text/html";
	}

	internal static bool IsRejected(HttpResponseMessage response) => response.StatusCode is HttpStatusCode.Forbidden && !IsBlocked(response);
}
