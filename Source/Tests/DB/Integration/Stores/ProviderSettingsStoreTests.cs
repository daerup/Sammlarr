using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;
using ProviderSettingsContract = Contracts.Settings.ProviderSettings;

namespace DB.Tests.Integration.Stores;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class ProviderSettingsStoreTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IProviderSettingsStore _testee = null!;
	private Mock<ITypedMemoryCache<string, ProviderSettings>> _cache = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_cache = ITypedMemoryCache<string, ProviderSettings>.Mock();
		_testee = new ProviderSettingsStore(fixture.Context, _cache.Object);
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
		var expected = CreateProviderSettings(WatchlistProvider.Plex, MediaRecommendationProvider.Tmdb, MediaSearchProvider.Plex);

		await _testee.StoreAsync(expected);

		var entity = await fixture.Context.ProviderSettings.SingleAsync();

		await Assert.That(entity.Id).IsEqualTo(ProviderSettingsEntity.SingletonId);
		await Assert.That(entity.WatchlistProvider).IsEqualTo(expected.WatchlistProvider);
		await Assert.That(entity.TrendingProvider).IsEqualTo(expected.TrendingProvider);
		await Assert.That(entity.SearchProvider).IsEqualTo(expected.SearchProvider);
	}

	[Test]
	public async Task StoreAsync_WhenCalledTwice_UpdatesSingletonRow()
	{
		var initial = CreateProviderSettings(WatchlistProvider.Plex, MediaRecommendationProvider.Tmdb, MediaSearchProvider.Plex);
		var updated = CreateProviderSettings(WatchlistProvider.Plex, MediaRecommendationProvider.Tmdb, MediaSearchProvider.Plex);

		await _testee.StoreAsync(initial);
		await _testee.StoreAsync(updated);

		var entities = await fixture.Context.ProviderSettings.ToListAsync();

		await Assert.That(entities).Count().IsEqualTo(1);
	}

	private static ProviderSettingsContract CreateProviderSettings(
		WatchlistProvider watchlistProvider,
		MediaRecommendationProvider trendingProvider,
		MediaSearchProvider searchProvider
	)
	{
		return new ProviderSettingsContract
		{
			WatchlistProvider = watchlistProvider,
			TrendingProvider = trendingProvider,
			SearchProvider = searchProvider,
		};
	}
}
