using Contracts;
using Domain;
using Domain.Auth;
using Domain.Providers;

namespace API.Tmdb;

internal sealed class TmdbApiKey : IApiKey
{
	private TmdbApiKey(string value) => Value = value;

	public ApiKeyProvider Provider => ApiKeyProvider.Tmdb;

	[Sensitive]
	public string Value { get; }

	public static async Task<Result<IApiKey>> CreateAsync(string value, TmdbClient client, CancellationToken cancellationToken = default)
	{
		if (!await client.ValidateApiKeyAsync(value, cancellationToken))
		{
			return Error.From("The TMDB API key is not valid.");
		}

		return new TmdbApiKey(value);
	}
}
