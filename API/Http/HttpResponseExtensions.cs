using System.Net;
using Domain.Providers;

namespace API.Http;

internal static class HttpResponseExtensions
{
	extension(HttpStatusCode status)
	{
		public Error Unanswered(RemoteService remote, string reason)
		{
			return IsTransient(status) ? Error.From(new RemoteUnavailableException(remote)) : Error.From(reason);
		}
	}

	private static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500;

	extension(Task<HttpResponseMessage> send)
	{
		public async Task<HttpResponseMessage> OnUnauthorizedAsync(Action invalidate)
		{
			var response = await send;
			if (response.StatusCode == HttpStatusCode.Unauthorized)
			{
				invalidate();
			}

			return response;
		}
	}
}
