using API.Mdblist;
using API.Tmdb;
using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;

namespace API.Auth;

internal sealed class ApiKeyFactory(IHttpClientFactory httpClientFactory, TmdbOptions tmdbOptions, MdblistOptions mdblistOptions) : IApiKeyFactory
{
	public Task<Result<IApiKey>> CreateAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default) =>
		provider switch
		{
			ApiKeyProvider.Mdblist => MdblistApiKey.CreateAsync(value, httpClientFactory, mdblistOptions, cancellationToken),
			ApiKeyProvider.Tmdb => TmdbApiKey.CreateAsync(value, httpClientFactory, tmdbOptions, cancellationToken),
		};
}
