using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Contracts.Auth;
using Contracts.Media;
using Shared.Options;

namespace API.Mdblist;

internal class MdblistClient(HttpClient client, MdblistOptions options, IApiKeyStore apiKeyStore) : IApiKeyValidator
{
	public async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		HttpContent? content = null,
		CancellationToken cancellationToken = default
	)
	{
		var apiKey =
			await apiKeyStore.GetApiKeyAsync(MediaProvider.Mdblist, cancellationToken)
			?? throw new InvalidOperationException("No MDBList API key is configured. Add one in Settings → API Keys.");

		return await SendWithKeyAsync(method, endpoint, apiKey, content, cancellationToken);
	}

	public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await SendWithKeyAsync(HttpMethod.Get, "user", apiKey, null, cancellationToken);
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
		query["apikey"] = apiKey;
		builder.Query = query.ToString() ?? string.Empty;

		using var request = new HttpRequestMessage(method, builder.Uri);
		request.Content = content;
		return client.SendAsync(request, cancellationToken);
	}
}
