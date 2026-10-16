using System.Net.Http.Headers;
using API.Http;
using Contracts.Auth;
using Domain.Auth;
using Shared.Options;

namespace API.Tmdb;

internal sealed class TmdbReadTokenCheck(IHttpClientFactory httpClientFactory, TmdbOptions options) : IApiKeyValidator
{
	public async Task<AccessCheck> CheckAsync(string value, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl.TrimEnd('/')}/{TmdbEndpoints.Authentication}");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", value);
		return await httpClientFactory.CreateClient(TmdbClient.Unauthenticated).CheckAccessAsync(request, cancellationToken);
	}
}
