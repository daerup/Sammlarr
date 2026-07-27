using API.Http;
using Contracts;
using Domain;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;

namespace API.Tmdb;

internal sealed class TmdbApiKey : IApiKey
{
	private TmdbApiKey(string value) => Value = value;

	public ApiKeyProvider Provider => ApiKeyProvider.Tmdb;

	[Sensitive]
	public string Value { get; }

	public static async Task<Result<IApiKey>> CreateAsync(
		string value,
		IHttpClientFactory httpClientFactory,
		TmdbOptions options,
		CancellationToken cancellationToken = default
	)
	{
		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			ApiKeyRequest.BuildUri(options.BaseUrl, TmdbEndpoints.Configuration, TmdbClient.QueryParam, value)
		);
		try
		{
			using var response = await httpClientFactory.CreateClient(TmdbClient.Unauthenticated).SendAsync(request, cancellationToken);
			return response.IsSuccessStatusCode ? Success.Of<IApiKey>(new TmdbApiKey(value)) : Error.From("The TMDB API key is not valid.");
		}
		catch (HttpRequestException)
		{
			return Error.From("The TMDB API key is not valid.");
		}
	}
}
