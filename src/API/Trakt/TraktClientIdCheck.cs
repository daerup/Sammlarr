using System.Net;
using API.Http.Lanes;
using Contracts.Auth;
using Domain.Auth;
using Polly;
using Shared.Options;

namespace API.Trakt;

internal sealed class TraktClientIdCheck(IHttpClientFactory httpClientFactory, TraktOptions options) : IApiKeyValidator
{
	public async Task<AccessCheck> CheckAsync(string value, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl.TrimEnd('/')}/genres/movies");
		TraktClient.Sign(request, value);
		request.Headers.UserAgent.ParseAdd(options.UserAgent);
		try
		{
			using var response = await httpClientFactory.CreateClient(TraktClient.Unauthenticated).SendAsync(request, cancellationToken);
			return response switch
			{
				{ IsSuccessStatusCode: true } => AccessCheck.Valid,
				_ when TraktClient.IsBlocked(response) => AccessCheck.Unreachable,
				{ StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => AccessCheck.Rejected,
				{ StatusCode: HttpStatusCode.TooManyRequests } => AccessCheck.Spent,
				_ => AccessCheck.Unreachable,
			};
		}
		catch (Exception exception)
			when (exception is HttpRequestException or ExecutionRejectedException or LaneRefusedException or TaskCanceledException
				&& !cancellationToken.IsCancellationRequested
			)
		{
			return AccessCheck.Unreachable;
		}
	}
}
