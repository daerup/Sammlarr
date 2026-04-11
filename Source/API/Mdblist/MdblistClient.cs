using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Contracts.Auth;

namespace API.Mdblist;

internal class MdblistClient(HttpClient client) : IApiKeyValidator
{
	public async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		string apiKey,
		HttpContent? content = null,
		CancellationToken cancellationToken = default
	)
	{
		var request = new HttpRequestMessage(method, endpoint);
		request.Headers.Add("Authorization", $"Bearer {apiKey}");
		request.Content = content;
		return await client.SendAsync(request, cancellationToken);
	}

	public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
	{
		try
		{
			// Example endpoint for validation, adjust as needed
			var response = await SendAsync(HttpMethod.Get, "https://mdblist.com/api/validate", apiKey, null, cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
