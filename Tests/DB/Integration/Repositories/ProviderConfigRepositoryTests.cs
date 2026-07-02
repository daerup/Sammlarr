using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using DB.Data.Config;
using Domain.Providers;
using Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class ProviderConfigRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private Mock<ITypedMemoryCache<string, ProviderConfig>> _cache = null!;
	private IProviderConfigRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_cache = ITypedMemoryCache<string, ProviderConfig>.Mock();
		_testee = new ProviderConfigRepository(fixture.ContextFactory, _cache.Object);
	}

	[Test]
	public async Task GetAsync_OnFreshDatabase_ReturnsSeededDefaults()
	{
		var providerSettings = await _testee.GetAsync();

		await Assert.That(providerSettings).IsNotNull();
		await Assert.That(providerSettings!.WatchlistProvider).IsEqualTo(WatchlistProvider.Plex);
		await Assert.That(providerSettings.TrendingProvider).IsEqualTo(MediaRecommendationProvider.Tmdb);
		await Assert.That(providerSettings.SearchProvider).IsEqualTo(MediaSearchProvider.Plex);
		_cache.TryGetValue(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_StoresAndRetrievesSuccessfully()
	{
		var expected = CreateProviderSettings(WatchlistProvider.Plex, MediaRecommendationProvider.Tmdb, MediaSearchProvider.Plex, RatingProvider.Tmdb);

		await _testee.StoreAsync(expected);

		var entity = await fixture.Context.ProviderSettings.SingleAsync();

		await Assert.That(entity.Id).IsEqualTo(ProviderConfigRow.SingletonId);
		await Assert.That(entity.WatchlistProvider).IsEqualTo(expected.WatchlistProvider);
		await Assert.That(entity.TrendingProvider).IsEqualTo(expected.TrendingProvider);
		await Assert.That(entity.SearchProvider).IsEqualTo(expected.SearchProvider);
		await Assert.That(entity.RatingProvider).IsEqualTo(expected.RatingProvider);
	}

	[Test]
	public async Task StoreAsync_WhenCalledTwice_UpdatesSingletonRow()
	{
		var initial = CreateProviderSettings(WatchlistProvider.Plex, MediaRecommendationProvider.Tmdb, MediaSearchProvider.Plex, RatingProvider.Tmdb);
		var updated = CreateProviderSettings(WatchlistProvider.Plex, MediaRecommendationProvider.Tmdb, MediaSearchProvider.Plex, RatingProvider.Tmdb);

		await _testee.StoreAsync(initial);
		await _testee.StoreAsync(updated);

		var entities = await fixture.Context.ProviderSettings.ToListAsync();

		await Assert.That(entities).Count().IsEqualTo(1);
	}

	private static ProviderConfig CreateProviderSettings(
		WatchlistProvider watchlistProvider,
		MediaRecommendationProvider trendingProvider,
		MediaSearchProvider searchProvider,
		RatingProvider ratingProvider
	) =>
		new()
		{
			WatchlistProvider = watchlistProvider,
			TrendingProvider = trendingProvider,
			SearchProvider = searchProvider,
			RatingProvider = ratingProvider,
		};
}
