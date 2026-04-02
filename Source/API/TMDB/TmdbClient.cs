using System.Web;
using Contracts.Media;
using Shared.Options;

namespace API.TMDB;

public class TmdbClient(HttpClient client, TmdbOptions options, IApiKeyStore apiKeyStore)
{
	public async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		CancellationToken cancellationToken = default,
		HttpContent? content = null
	)
	{
		var apiKey =
			await apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, cancellationToken)
			?? throw new InvalidOperationException("No TMDB API key is configured. Add one in Settings → API Keys.");

		return await SendWithKeyAsync(method, endpoint, apiKey, content, cancellationToken);
	}

	public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await SendWithKeyAsync(HttpMethod.Get, "configuration", apiKey, null, cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

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
