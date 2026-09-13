using Domain.Providers;

namespace Domain.Tests.Providers;

public class ProviderMatrixTests
{
	[Test]
	public async Task Values_OnASubsetEnum_AreOnlyThatCapabilitysBrands()
	{
		await Assert.That(Enum.GetValues<SearchProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<WatchlistProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<TrendingProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<RatingProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<FeedProvider>().Length).IsEqualTo(2);
		await Assert.That(Enum.GetValues<ApiKeyProvider>().Length).IsEqualTo(2);
	}

	[Test]
	public async Task ToMediaProvider_FromASubsetProvider_CollapsesToTheBrand()
	{
		await Assert.That(WatchlistProvider.Plex.AsMediaProvider()).IsEqualTo(MediaProvider.Plex);
		await Assert.That(SearchProvider.Plex.AsMediaProvider()).IsEqualTo(MediaProvider.Plex);
		await Assert.That(RatingProvider.Tmdb.AsMediaProvider()).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(FeedProvider.Mdblist.AsMediaProvider()).IsEqualTo(MediaProvider.Mdblist);
	}

	[Test]
	public async Task Convert_AcrossCapabilities_FollowsTheBrand()
	{
		await Assert.That(TrendingProvider.Tmdb.AsApiKeyProvider()).IsEqualTo(ApiKeyProvider.Tmdb);
		await Assert.That(FeedProvider.Tmdb.AsApiKeyProvider()).IsEqualTo(ApiKeyProvider.Tmdb);
		await Assert.That(ApiKeyProvider.Tmdb.AsRatingProvider()).IsEqualTo(RatingProvider.Tmdb);
	}

	[Test]
	public async Task Convert_WhenTheBrandLacksTheCapability_IsNull()
	{
		await Assert.That(ApiKeyProvider.Mdblist.AsRatingProvider()).IsNull();
	}

	[Test]
	public async Task Description_OnAGeneratedEnum_MatchesTheProvider()
	{
		await Assert.That(WatchlistProvider.Plex.GetDescription()).IsEqualTo("Plex");
		await Assert.That(TrendingProvider.Tmdb.GetDescription()).IsEqualTo("TMDB");
		await Assert.That(AuthProvider.Plex.GetDescription()).IsEqualTo("Plex");
	}
}
