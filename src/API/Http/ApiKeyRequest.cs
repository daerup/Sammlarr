using System.Web;

namespace API.Http;

internal static class ApiKeyRequest
{
	public static Uri BuildUri(string baseUrl, string endpoint, string queryParam, string apiKey)
	{
		var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
		var requestUri = Uri.TryCreate(endpoint, UriKind.Absolute, out var absolute) ? absolute : new Uri(baseUri, endpoint);

		var builder = new UriBuilder(requestUri);
		var query = HttpUtility.ParseQueryString(builder.Query);
		query[queryParam] = apiKey;
		builder.Query = query.ToString() ?? string.Empty;

		return builder.Uri;
	}
}
