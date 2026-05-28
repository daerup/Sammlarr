using Contracts.Auth;
using Contracts.Media;

namespace Integration.Tests.Infrastructure;

[Binding]
public class AccountSetupSteps(SammlarrScenarioContext sammlarr)
{
	[Given("a TMDB account with API key {string}")]
	public async Task GivenTmdbAccount(string apiKey)
	{
		await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(MediaProvider.Tmdb, apiKey);
		sammlarr.HttpHandler.RequireQueryParam(TestUrls.TmdbApi, "api_key", apiKey);
	}

	[Given("an MdbList account with API key {string}")]
	public async Task GivenMdblistAccount(string apiKey)
	{
		await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(MediaProvider.Mdblist, apiKey);
		sammlarr.HttpHandler.RequireQueryParam(TestUrls.MdblistApi, "apikey", apiKey);
	}

	[Given("a Plex account with token {string}")]
	public async Task GivenPlexAccount(string token)
	{
		await sammlarr.Resolve<ICredentialStore>().StoreAsync(WatchlistProvider.Plex, token, DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());
		sammlarr.HttpHandler.RequireHeader(TestUrls.PlexApi, "X-Plex-Token", token);
	}
}
