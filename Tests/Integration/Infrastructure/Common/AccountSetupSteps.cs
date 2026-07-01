using Contracts.Auth;
using Contracts.Media;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Http;
using Test.Shared;

namespace Integration.Tests.Infrastructure.Common;

[Binding]
public class AccountSetupSteps(SammlarrScenarioContext sammlarr)
{
	[Given("a TMDB account with API key {string}")]
	public async Task GivenTmdbAccount(string apiKey)
	{
		await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, apiKey));
		sammlarr.HttpHandler.RequireQueryParam(TestUrls.TmdbApi, "api_key", apiKey);
	}

	[Given("an MdbList account with API key {string}")]
	public async Task GivenMdblistAccount(string apiKey)
	{
		await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Mdblist, apiKey));
		sammlarr.HttpHandler.RequireQueryParam(TestUrls.MdblistApi, "apikey", apiKey);
	}

	[Given("a Plex account with token {string}")]
	public async Task GivenPlexAccount(string token)
	{
		await sammlarr.Resolve<ICredentialRepository>().StoreAsync(WatchlistProvider.Plex, token, DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());
		sammlarr.HttpHandler.RequireHeader(TestUrls.PlexApi, "X-Plex-Token", token);
	}
}
