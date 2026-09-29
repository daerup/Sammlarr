using Contracts.Auth;
using Contracts.Feed;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Harness;
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
	public async Task SaveApiKeyAsync_ThenGetApiKeyAsync_ReturnsTheSavedKey()
	{
		await Testee.SaveApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key-12345"));

		await Assert
			.That(await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("tmdb-key-12345"));
	}

	[Test]
	public async Task GetFeedProvidersWithoutKeyAsync_WithOnlyAnMdblistKey_ListsTheFeedProvidersThatNeedTheOthers()
	{
		await Testee.SaveApiKeyAsync(new TestApiKey(ApiKeyProvider.Mdblist, "mdblist-key-12345"));

		await Assert.That(await Testee.GetFeedProvidersWithoutKeyAsync()).IsEquivalentTo([FeedProvider.Tmdb]);
	}

	[Test]
	public async Task RemoveApiKeyAsync_WhenAKeyIsStored_RemovesIt()
	{
		await ApiKeys.SetApiKeyAsync(ApiKeyProvider.Tmdb, "tmdb-key-12345");

		await Testee.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task RemoveApiKeyAsync_DisablesAndUnschedulesEveryFeedThatNeedsTheKey()
	{
		var trending = await Feeds.CreateFeedAsync(Feed("Trending movies", FeedProvider.Tmdb));

		await Testee.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That((await Feeds.GetFeedByIdAsync(trending))!.Enabled).IsFalse();
		await Assert.That(Scheduler.Unscheduled).Contains(trending);
	}

	[Test]
	public async Task RemoveApiKeyAsync_LeavesFeedsOnOtherKeysRunning()
	{
		var documentaries = await Feeds.CreateFeedAsync(Feed("Documentaries", FeedProvider.Mdblist));

		await Testee.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That((await Feeds.GetFeedByIdAsync(documentaries))!.Enabled).IsTrue();
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(documentaries);
	}

	[Test]
	public async Task RemoveApiKeyAsync_WhenAnotherUserRemovesTheirKey_LeavesYourFeedsRunning()
	{
		var yours = await Feeds.CreateFeedAsync(Feed("Trending movies", FeedProvider.Tmdb));
		var owner = host.CurrentUser.Id;
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededMember.Id);

		await Testee.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		host.CurrentUser.Id = owner;
		await Assert.That((await Feeds.GetFeedByIdAsync(yours))!.Enabled).IsTrue();
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(yours);
	}

	private static FeedCreateData Feed(string name, FeedProvider provider) =>
		new()
		{
			Name = name,
			IsEnabled = true,
			Provider = provider,
			Strategy = new FeedStrategy(new Append()),
			Identifier = name.ToLowerInvariant(),
			Schedule = new FeedRunTrigger(new Scheduled(Nightly)),
		};
}
