using Contracts.Auth;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserIsolationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WantlistItemProvenance Remote = new(new SinkOrigin(SinkKind.Plex));

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Feeds_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Resolve<IFeedRepository>()
			.CreateFeedAsync(
				new FeedCreateData
				{
					Name = "Weekly",
					IsEnabled = true,
					Provider = FeedProvider.Mdblist,
					Strategy = new FeedStrategy(new Append()),
					Identifier = "list",
					Schedule = new FeedRunTrigger(new Manual()),
				}
			);

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.Resolve<IFeedRepository>().GetFeedsAsync()).IsEmpty();

		host.SignOut();
		await host.SignInAs("Alice");
		var feeds = await host.Resolve<IFeedRepository>().GetFeedsAsync();
		await Assert.That(feeds.Any(feed => feed.Name == "Weekly")).IsTrue();
	}

	[Test]
	public async Task ApiKeys_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(ApiKeyProvider.Tmdb, "tmdb-secret");

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.CountOwnedAsync(context => context.ApiKeySettings)).IsEqualTo(0);
	}

	[Test]
	public async Task Credentials_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Resolve<ICredentialRepository>().StoreAsync(AuthProvider.Plex, "plex-token", DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.CountOwnedAsync(context => context.Credentials)).IsEqualTo(0);
	}

	[Test]
	public async Task WantlistEntries_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.Resolve<IWantlistService>().GetWantlistIdsAsync()).IsEmpty();
	}

	[Test]
	public async Task BlocklistEntries_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Movie(TestMedia.Inception).OnBlocklist();

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.Resolve<IBlocklistService>().GetBlocklistIdsAsync()).IsEmpty();
	}
}
