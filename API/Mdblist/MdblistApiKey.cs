using API.Http;
using Contracts;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistApiKey : IApiKey
{
	private MdblistApiKey(string value) => Value = value;

	public ApiKeyProvider Provider => ApiKeyProvider.Mdblist;

	[Sensitive]
	public string Value { get; }

	public static async Task<Result<IApiKey>> CreateAsync(
		string value,
		IHttpClientFactory httpClientFactory,
		MdblistOptions options,
		CancellationToken cancellationToken = default
	)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, ApiKeyRequest.BuildUri(options.BaseUrl, MdblistEndpoints.User, MdblistClient.QueryParam, value));
		try
		{
			using var response = await httpClientFactory.CreateClient(MdblistClient.Unauthenticated).SendAsync(request, cancellationToken);
			return response.IsSuccessStatusCode ? Success.Of<IApiKey>(new MdblistApiKey(value)) : Error.From("The MDBList API key is not valid.");
		}
		catch (HttpRequestException)
		{
			return Error.From("The MDBList API key is not valid.");
		}
	}
}
