using Domain.Providers;

namespace Domain.Tests.Providers;

public class ProviderMatrixTests
{
	[Test]
	public async Task Subset_enums_contain_only_their_capability_brands()
	{
		await Assert.That(Enum.GetValues<SearchProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<WatchlistProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<TrendingProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<RatingProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<FeedProvider>().Length).IsEqualTo(2);
		await Assert.That(Enum.GetValues<ApiKeyProvider>().Length).IsEqualTo(2);
	}

	[Test]
	public async Task Subset_providers_collapse_to_their_media_provider_brand()
	{
		await Assert.That(WatchlistProvider.Plex.AsMediaProvider()).IsEqualTo(MediaProvider.Plex);
		await Assert.That(SearchProvider.Plex.AsMediaProvider()).IsEqualTo(MediaProvider.Plex);
		await Assert.That(RatingProvider.Tmdb.AsMediaProvider()).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(FeedProvider.Mdblist.AsMediaProvider()).IsEqualTo(MediaProvider.Mdblist);
	}

	[Test]
	public async Task Cross_capability_conversions_follow_the_brand()
	{
		await Assert.That(TrendingProvider.Tmdb.AsApiKeyProvider()).IsEqualTo(ApiKeyProvider.Tmdb);
		await Assert.That(FeedProvider.Tmdb.AsApiKeyProvider()).IsEqualTo(ApiKeyProvider.Tmdb);
		await Assert.That(ApiKeyProvider.Tmdb.AsRatingProvider()).IsEqualTo(RatingProvider.Tmdb);
	}

	[Test]
	public async Task Cross_capability_conversion_is_null_when_the_brand_lacks_the_capability()
	{
		await Assert.That(ApiKeyProvider.Mdblist.AsRatingProvider()).IsNull();
	}

	[Test]
	public async Task Generated_enums_carry_the_provider_description()
	{
		await Assert.That(WatchlistProvider.Plex.GetDescription()).IsEqualTo("Plex");
		await Assert.That(TrendingProvider.Tmdb.GetDescription()).IsEqualTo("TMDB");
		await Assert.That(AuthProvider.Plex.GetDescription()).IsEqualTo("Plex");
	}
}
