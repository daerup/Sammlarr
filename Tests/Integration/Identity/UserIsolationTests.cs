using Contracts.Auth;
using Contracts.Blacklist;
using Contracts.Feed;
using Contracts.Watchlist;
using Domain.Feed;
using Domain.Providers;
using Domain.Watchlist;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserIsolationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WatchlistItemProvenance Remote = new(new RemoteOrigin(WatchlistProvider.Plex));

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
		await host.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-secret"));

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
	public async Task WatchlistEntries_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.Resolve<IWatchlistService>().GetWatchlistIdsAsync()).IsEmpty();
	}

	[Test]
	public async Task BlacklistEntries_WhenTwoUsersExist_ArePrivateToEach()
	{
		await host.SignInAs("Alice");
		await host.Movie(TestMedia.Inception).OnBlacklist();

		host.SignOut();
		await host.SignInAs("Bob");
		await Assert.That(await host.Resolve<IBlacklistService>().GetBlacklistIdsAsync()).IsEmpty();
	}
}
