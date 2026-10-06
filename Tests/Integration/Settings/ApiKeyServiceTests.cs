using Contracts.Auth;
using Contracts.Feed;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Harness;
using Integration.Tests.Feeds;
using TestInfrastructure;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ApiKeyServiceTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Nightly = "0 0 3 * * *";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IApiKeyService Testee => host.Resolve<IApiKeyService>();

	private IApiKeyConfigRepository ApiKeys => host.Resolve<IApiKeyConfigRepository>();

	private IFeedService Feeds => host.Resolve<IFeedService>();

	private RecordingFeedScheduler Scheduler => host.Resolve<RecordingFeedScheduler>();

	[Test]
	public async Task StoreAsync_ThenGetAsync_ReturnsTheSavedKey()
	{
		await Testee.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key-12345"));

		await Assert
			.That(await Testee.GetAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("tmdb-key-12345"));
	}

	[Test]
	public async Task ClearAsync_WhenAKeyIsStored_RemovesIt()
	{
		await ApiKeys.StoreAsync(ApiKeyProvider.Tmdb, "tmdb-key-12345");

		await Testee.ClearAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task ClearAsync_DisablesAndUnschedulesEveryFeedThatNeedsTheKey()
	{
		var trending = await Feeds.CreatedAsync(Feed("Trending movies", FeedProvider.Mdblist));

		await Testee.ClearAsync(ApiKeyProvider.Mdblist);

		await Assert.That((await host.FeedAsync(trending)).Enabled).IsFalse();
		await Assert.That(Scheduler.Unscheduled).Contains(trending);
	}

	[Test]
	public async Task ClearAsync_LeavesFeedsOnOtherKeysRunning()
	{
		var documentaries = await Feeds.CreatedAsync(Feed("Documentaries", FeedProvider.Mdblist));

		await Testee.ClearAsync(ApiKeyProvider.Tmdb);

		await Assert.That((await host.FeedAsync(documentaries)).Enabled).IsTrue();
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(documentaries);
	}

	[Test]
	public async Task ClearAsync_WhenAnotherUserRemovesTheirKey_LeavesYourFeedsRunning()
	{
		var yours = await Feeds.CreatedAsync(Feed("Trending movies", FeedProvider.Mdblist));
		var owner = host.CurrentUser.Id;
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededMember.Id);

		await Testee.ClearAsync(ApiKeyProvider.Mdblist);

		host.CurrentUser.Id = owner;
		await Assert.That((await host.FeedAsync(yours)).Enabled).IsTrue();
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(yours);
	}

	private static FeedCreateData Feed(string name, FeedProvider provider) =>
		new()
		{
			Name = name,
			IsEnabled = true,
			Provider = provider,
			Strategy = new FeedStrategy(new Append()),
			Identifier = FeedRunSeeding.ListOf(name),
			Schedule = new FeedRunTrigger(new Scheduled(Nightly)),
		};
}
