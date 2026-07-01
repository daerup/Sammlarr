using System.Web;
using Shared.Options;

namespace API.Tmdb;

internal class TmdbClient(HttpClient client, TmdbOptions options)
{
	public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await SendWithKeyAsync(HttpMethod.Get, TmdbEndpoints.Configuration, apiKey, null, cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	public Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		IApiKey apiKey,
		CancellationToken cancellationToken = default,
		HttpContent? content = null
	) => SendWithKeyAsync(method, endpoint, apiKey.Value, content, cancellationToken);

	private Task<HttpResponseMessage> SendWithKeyAsync(
		HttpMethod method,
		string endpoint,
		string apiKey,
		HttpContent? content,
		CancellationToken cancellationToken = default
	)
	{
		var baseUri = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
		var requestUri = Uri.TryCreate(endpoint, UriKind.Absolute, out var absolute) ? absolute : new Uri(baseUri, endpoint);

		var builder = new UriBuilder(requestUri);
		var query = HttpUtility.ParseQueryString(builder.Query);
		query["api_key"] = apiKey;
		builder.Query = query.ToString() ?? string.Empty;

		using var request = new HttpRequestMessage(method, builder.Uri);
		request.Content = content;
		return client.SendAsync(request, cancellationToken);
	}
}
