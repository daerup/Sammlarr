using System.Net;
using API.Http.Lanes;
using Domain.Auth;
using Polly;

namespace API.Http;

internal static class AccessCheckExtensions
{
	extension(HttpClient client)
	{
		public async Task<AccessCheck> CheckAccessAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			try
			{
				using var response = await client.SendAsync(request, cancellationToken);
				return response.IsSuccessStatusCode ? AccessCheck.Valid : Refused(response.StatusCode);
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

	private static AccessCheck Refused(HttpStatusCode status)
	{
		return status switch
		{
			HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AccessCheck.Rejected,
			HttpStatusCode.TooManyRequests => AccessCheck.Spent,
			_ => AccessCheck.Unreachable,
		};
	}
}
