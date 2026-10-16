using API.Http;
using Contracts.Auth;
using Domain.Auth;
using Shared.Options;

namespace API.Tmdb;

internal sealed class TmdbKeyCheck(IHttpClientFactory httpClientFactory, TmdbOptions options) : IApiKeyValidator
{
	public async Task<AccessCheck> CheckAsync(string value, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			ApiKeyRequest.BuildUri(options.BaseUrl, TmdbEndpoints.Configuration, TmdbClient.QueryParam, value)
		);
		return await httpClientFactory.CreateClient(TmdbClient.Unauthenticated).CheckAccessAsync(request, cancellationToken);
	}
}
