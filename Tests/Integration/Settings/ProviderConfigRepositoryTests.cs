using Contracts.Settings;
using DB.Data.Config;
using Domain.Providers;
using Domain.Settings;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ProviderConfigRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IProviderConfigRepository Testee => new ProviderConfigRepository(fixture.ContextFactory);

	[Test]
	public async Task GetAsync_OnFreshDatabase_ReturnsSeededDefaults()
	{
		var providerSettings = await Testee.GetAsync();

		await Assert.That(providerSettings).IsNotNull();
		await Assert.That(providerSettings.WatchlistProvider).IsEqualTo(WatchlistProvider.Plex);
		await Assert.That(providerSettings.TrendingProvider).IsEqualTo(TrendingProvider.Tmdb);
		await Assert.That(providerSettings.SearchProvider).IsEqualTo(SearchProvider.Plex);
		await Assert.That(providerSettings.AuthProvider).IsEqualTo(AuthProvider.Plex);
	}

	[Test]
	public async Task StoreAsync_StoresAndRetrievesSuccessfully()
	{
		var expected = CreateProviderSettings(WatchlistProvider.Plex, TrendingProvider.Tmdb, SearchProvider.Plex, RatingProvider.Tmdb);

		await Testee.StoreAsync(expected);

		var entity = await fixture.Context.ProviderSettings.SingleAsync();

		await Assert.That(entity.WatchlistProvider).IsEqualTo(expected.WatchlistProvider);
		await Assert.That(entity.TrendingProvider).IsEqualTo(expected.TrendingProvider);
		await Assert.That(entity.SearchProvider).IsEqualTo(expected.SearchProvider);
		await Assert.That(entity.RatingProvider).IsEqualTo(expected.RatingProvider);
	}

	[Test]
	public async Task StoreAsync_WhenCalledTwice_UpdatesSingletonRow()
	{
		var initial = CreateProviderSettings(WatchlistProvider.Plex, TrendingProvider.Tmdb, SearchProvider.Plex, RatingProvider.Tmdb);
		var updated = CreateProviderSettings(WatchlistProvider.Plex, TrendingProvider.Tmdb, SearchProvider.Plex, RatingProvider.Tmdb);

		await Testee.StoreAsync(initial);
		await Testee.StoreAsync(updated);

		var entities = await fixture.Context.ProviderSettings.ToListAsync();

		await Assert.That(entities).Count().IsEqualTo(1);
	}

	private static ProviderConfig CreateProviderSettings(
		WatchlistProvider watchlistProvider,
		TrendingProvider trendingProvider,
		SearchProvider searchProvider,
		RatingProvider ratingProvider
	) =>
		new()
		{
			WatchlistProvider = watchlistProvider,
			TrendingProvider = trendingProvider,
			SearchProvider = searchProvider,
			RatingProvider = ratingProvider,
			AuthProvider = AuthProvider.Plex,
			RegistrationMode = RegistrationMode.Open,
		};
}
