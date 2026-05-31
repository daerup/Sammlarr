using System.Net;
using API.Plex;
using Contracts;
using Contracts.Auth;
using Integration.Tests.Infrastructure.Hosting;

namespace Integration.Tests.Settings;

[Binding]
public class PlexAuthSteps(PlexAuthScenarioContext ctx, SammlarrScenarioContext sammlarr)
{
	[Given("plex reports the signed-in user {string}")]
	public void GivenPlexUser(string name) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl(
			PlexEndpoints.UserPath,
			$$"""{ "username": "{{name}}", "title": "{{name}}", "friendlyName": "{{name}}" }"""
		);

	[Given("plex rejects the user request")]
	public void GivenPlexRejects() => sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.UserPath, HttpStatusCode.Unauthorized);

	[When("I look up the signed-in Plex user")]
	public async Task WhenLookUpUser() => ctx.User = await sammlarr.ResolveKeyed<IOAuthService>(WatchlistProvider.Plex).GetLoggedInUser();

	[Then("the signed-in Plex user is {string}")]
	public async Task ThenUserIs(string name)
	{
		await Assert.That(ctx.User).IsNotNull();
		await Assert.That(ctx.User!.DisplayName).IsEqualTo(name);
	}

	[Then("there is no signed-in Plex user")]
	public async Task ThenNoUser() => await Assert.That(ctx.User).IsNull();
}
