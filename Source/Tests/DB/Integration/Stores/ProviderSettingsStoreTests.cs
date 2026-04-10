using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Settings;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;
using TUnit.Mocks.Generated.Contracts.Caching;
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
		await Assert.That(providerSettings!.WatchlistProvider).IsEqualTo(AuthProvider.Plex);
		await Assert.That(providerSettings.TrendingProvider).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(providerSettings.SearchProvider).IsEqualTo(MediaProvider.Plex);
		_cache.TryGetValue(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_StoresAndRetrievesSuccessfully()
	{
		var expected = CreateProviderSettings(AuthProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb);

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
		var initial = CreateProviderSettings(AuthProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb);
		var updated = CreateProviderSettings(AuthProvider.Plex, MediaProvider.Imdb, MediaProvider.Tmdb);

		await _testee.StoreAsync(initial);
		await _testee.StoreAsync(updated);

		var entities = await fixture.Context.ProviderSettings.ToListAsync();

		await Assert.That(entities).Count().IsEqualTo(1);
		await Assert.That(entities.Single().WatchlistProvider).IsEqualTo(updated.WatchlistProvider);
		await Assert.That(entities.Single().TrendingProvider).IsEqualTo(updated.TrendingProvider);
		await Assert.That(entities.Single().SearchProvider).IsEqualTo(updated.SearchProvider);
	}

	private static ProviderSettingsContract CreateProviderSettings(AuthProvider watchlistProvider, MediaProvider trendingProvider, MediaProvider searchProvider)
	{
		return new ProviderSettingsContract
		{
			WatchlistProvider = watchlistProvider,
			TrendingProvider = trendingProvider,
			SearchProvider = searchProvider,
		};
	}
}
