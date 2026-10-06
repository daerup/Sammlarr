using API.Http;
using Contracts.Auth;
using Domain.Auth;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistKeyCheck(IHttpClientFactory httpClientFactory, MdblistOptions options) : IApiKeyValidator
{
	public async Task<AccessCheck> CheckAsync(string value, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, ApiKeyRequest.BuildUri(options.BaseUrl, MdblistEndpoints.User, MdblistClient.QueryParam, value));
		return await httpClientFactory.CreateClient(MdblistClient.Unauthenticated).CheckAccessAsync(request, cancellationToken);
	}
}
