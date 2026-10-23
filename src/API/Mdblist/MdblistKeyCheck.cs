using API.Http;
using API.Http.Lanes;
using Contracts.Auth;
using Contracts.Providers;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistKeyCheck(IHttpClientFactory httpClientFactory, MdblistOptions options, MdblistKeyFlights flights) : IApiKeyValidator
{
	public Task<AccessCheck> CheckAsync(Secret value, CancellationToken cancellationToken = default)
	{
		return flights.JoinAsync(value, () => SendAsync(value)).WaitAsync(cancellationToken);
	}

	private async Task<AccessCheck> SendAsync(Secret value)
	{
		using var _ = LanePriority.Next(LaneClass.Source).Enter();
		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			ApiKeyRequest.BuildUri(options.BaseUrl, MdblistEndpoints.User, MdblistClient.QueryParam, value.Reveal())
		);
		request.Options.Set(LaneHandler.Owner, value.Reveal());
		return await httpClientFactory.CreateClient(MdblistClient.Unauthenticated).CheckAccessAsync(request, CancellationToken.None);
	}
}
