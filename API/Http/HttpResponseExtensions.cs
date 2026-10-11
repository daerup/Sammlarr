using System.Net;
using Domain.Providers;

namespace API.Http;

internal static class HttpResponseExtensions
{
	extension(HttpStatusCode status)
	{
		public Error Unanswered(RemoteService remote, string reason)
		{
			return status switch
			{
				HttpStatusCode.NotFound => Error.From(reason),
				HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError => Error.From(
					new RemoteUnavailableException(remote, sent: true)
				),
				_ => Error.From(reason, new HttpRequestException(reason, null, status)),
			};
		}
	}

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
