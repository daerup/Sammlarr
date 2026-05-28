using Contracts.Auth;
using Contracts.Media;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Settings;

[Binding]
public class ApiKeyManagementSteps(ApiKeyManagementScenarioContext ctx, SammlarrScenarioContext sammlarr)
{
	[Given("the TMDB API key is {string}")]
	public async Task GivenApiKeyIsSet(string apiKey) => await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(MediaProvider.Tmdb, apiKey);

	[When("I get the API key for TMDB")]
	public async Task WhenGetApiKey() => ctx.RetrievedKey = await sammlarr.Resolve<IApiKeyConfigRepository>().GetApiKeyAsync(MediaProvider.Tmdb);

	[When("I set the TMDB API key to {string}")]
	public async Task WhenSetApiKey(string apiKey) => await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(MediaProvider.Tmdb, apiKey);

	[When("I clear the TMDB API key")]
	public async Task WhenClearApiKey() => await sammlarr.Resolve<IApiKeyConfigRepository>().RemoveApiKeyAsync(MediaProvider.Tmdb);

	[Then("the API key is null")]
	public async Task ThenApiKeyIsNull() => await Assert.That(ctx.RetrievedKey).IsNull();

	[Then("the API key is {string}")]
	public async Task ThenApiKeyIs(string expected) => await Assert.That(ctx.RetrievedKey).IsEqualTo(expected);
}
