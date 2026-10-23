using API.Http;
using Contracts.Auth;
using Domain.Auth;
using Shared.Options;

namespace API.Mal;

internal sealed class MalClientIdCheck(IHttpClientFactory httpClientFactory, MalOptions options) : IApiKeyValidator
{
	public async Task<AccessCheck> CheckAsync(Secret value, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl.TrimEnd('/')}/anime/1?fields=id");
		request.Headers.Add(MalClient.ClientIdHeader, value.Reveal());
		return await httpClientFactory.CreateClient(MalClient.Unauthenticated).CheckAccessAsync(request, cancellationToken);
	}
}
