using System.Net;

namespace API.Http;

internal static class HttpResponseExtensions
{
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
