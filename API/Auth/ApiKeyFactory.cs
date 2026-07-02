using API.Mdblist;
using API.Tmdb;
using Contracts;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;

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
