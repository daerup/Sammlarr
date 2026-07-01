using API.Mdblist;
using API.Tmdb;
using Contracts;
using Contracts.Auth;

namespace API.Auth;

internal sealed class ApiKeyFactory(MdblistClient mdblistClient, TmdbClient tmdbClient) : IApiKeyFactory
{
	public Task<Result<IApiKey>> CreateAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default) =>
		provider switch
		{
			ApiKeyProvider.Mdblist => MdblistApiKey.CreateAsync(value, mdblistClient, cancellationToken),
			ApiKeyProvider.Tmdb => TmdbApiKey.CreateAsync(value, tmdbClient, cancellationToken),
		};
}
