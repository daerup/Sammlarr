using System.Net;
using API.Plex;
using Contracts;
using Contracts.Auth;
using Integration.Tests.Infrastructure.Hosting;

namespace Integration.Tests.Settings;

[Binding]
public class PlexAuthSteps(PlexAuthScenarioContext ctx, SammlarrScenarioContext sammlarr)
{
	[Given("Plex reports a signed-in user with friendly name {string}, title {string}, username {string}")]
	public void GivenPlexUserNames(string friendlyName, string title, string username) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl(
			PlexEndpoints.UserPath,
			$$"""{ "username": "{{username}}", "title": "{{title}}", "friendlyName": "{{friendlyName}}" }"""
		);

	[Given("Plex reports the signed-in user {string} with active subscription {string}")]
	public void GivenPlexUserActiveSubscription(string name, string description) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl(
			PlexEndpoints.UserPath,
			$$"""{ "username": "{{name}}", "title": "{{name}}", "friendlyName": "{{name}}", "subscription": { "active": true }, "subscriptionDescription": "{{description}}" }"""
		);

	[Given("Plex reports the signed-in user {string} with inactive subscription")]
	public void GivenPlexUserInactiveSubscription(string name) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl(
			PlexEndpoints.UserPath,
			$$"""{ "username": "{{name}}", "title": "{{name}}", "friendlyName": "{{name}}", "subscription": { "active": false }, "subscriptionDescription": "ignored" }"""
		);

	[Given("Plex rejects the user request")]
	public void GivenPlexRejects() => sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.UserPath, HttpStatusCode.Unauthorized);

	[Given("Plex returns a server error for the user request")]
	public void GivenPlexUserServerError() => sammlarr.HttpHandler.RespondWithStatusForUrl(PlexEndpoints.UserPath, HttpStatusCode.InternalServerError);

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

	[Then("the signed-in Plex user subscription description is {string}")]
	public async Task ThenSubscriptionDescriptionIs(string description)
	{
		await Assert.That(ctx.User).IsNotNull();
		await Assert.That(ctx.User!.SubscriptionDescription).IsEqualTo(description);
	}

	[Then("the signed-in Plex user subscription description is empty")]
	public async Task ThenSubscriptionDescriptionIsEmpty()
	{
		await Assert.That(ctx.User).IsNotNull();
		await Assert.That(ctx.User!.SubscriptionDescription).IsEmpty();
	}
}
