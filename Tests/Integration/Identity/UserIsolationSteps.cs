using System.Security.Cryptography;
using System.Text;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Settings;
using DB;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Integration.Tests.Infrastructure.Hosting;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using TestInfrastructure;

namespace Integration.Tests.Identity;

[Binding]
public class UserIsolationSteps(SammlarrScenarioContext sammlarr)
{
	[Given("I am signed in as {string}")]
	[When("I sign in as {string}")]
	public void SignInAs(string name)
	{
		var current = sammlarr.Resolve<TestCurrentUser>();
		current.Id = UserId.From(DeterministicGuid(name));
		current.DisplayName = name;
		current.Role = UserRole.Member;
		current.IsAuthenticated = true;
		sammlarr.SeedUser(current);
	}

	[When("I sign out")]
	public void SignOut() => sammlarr.Resolve<TestCurrentUser>().IsAuthenticated = false;

	[Given("a feed named {string}")]
	public async Task CreateFeed(string name) =>
		await sammlarr
			.Resolve<IFeedRepository>()
			.CreateFeedAsync(
				name,
				new FeedCreateData
				{
					IsEnabled = true,
					Provider = FeedProvider.Mdblist,
					Strategy = FeedAddStrategy.Append,
					Identifier = "list",
					Schedule = new SyncMode(new Manual()),
				}
			);

	[Then("I have no feeds")]
	public async Task NoFeeds() => await Assert.That(await sammlarr.Resolve<IFeedRepository>().GetFeedsAsync()).IsEmpty();

	[Then("the feed {string} is listed")]
	public async Task FeedListed(string name)
	{
		var feeds = await sammlarr.Resolve<IFeedRepository>().GetFeedsAsync();
		await Assert.That(feeds.Any(feed => feed.Name == name)).IsTrue();
	}

	[Given("a stored TMDB API key")]
	public async Task StoreApiKey() => await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-secret"));

	[Then("I have no stored API keys")]
	public async Task NoApiKeys() => await Assert.That(await CountAsync(ctx => ctx.ApiKeySettings)).IsEqualTo(0);

	[Given("a stored Plex credential")]
	public async Task StoreCredential() =>
		await sammlarr.Resolve<ICredentialRepository>().StoreAsync(AuthProvider.Plex, "plex-token", DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());

	[Then("I have no stored credentials")]
	public async Task NoCredentials() => await Assert.That(await CountAsync(ctx => ctx.Credentials)).IsEqualTo(0);

	private async Task<int> CountAsync<T>(Func<SammlarrDbContext, IQueryable<T>> set)
	{
		await using var ctx = sammlarr.Resolve<IDbContextFactory<SammlarrDbContext>>().CreateDbContext();
		return await set(ctx).CountAsync();
	}

	private static Guid DeterministicGuid(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}
