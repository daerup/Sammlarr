using Domain.Providers;
using TestInfrastructure;

namespace Domain.Tests.Providers;

public class ProviderMatrixTests
{
	[Test]
	public async Task Values_OnASubsetEnum_AreOnlyThatCapabilitysBrands()
	{
		await Assert.That(Enum.GetValues<SinkKind>().Length).IsEqualTo(2);
		await Assert.That(Enum.GetValues<TrendingProvider>().Length).IsEqualTo(1);
		await Assert.That(Enum.GetValues<FeedProvider>().Length).IsEqualTo(2);
		await Assert.That(Enum.GetValues<ApiKeyProvider>().Length).IsEqualTo(2);
		await Assert.That(Enum.GetValues<ArtworkProvider>().Length).IsEqualTo(3);
		await Assert.That(Enum.GetValues<EnricherProvider>().Length).IsEqualTo(2);
	}

	[Test]
	public async Task ToMediaProvider_FromASubsetProvider_CollapsesToTheBrand()
	{
		await Assert.That(FeedProvider.Mdblist.AsMediaProvider()).IsEqualTo(MediaProvider.Mdblist);
	}

	[Test]
	public async Task Provider_OfASearch_IsTheServiceBehindIt()
	{
		await Assert.That(SearchProvider.Tmdb.Provider()).IsSome().And.IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(SearchProvider.Plex.Provider()).IsSome().And.IsEqualTo(MediaProvider.Plex);
		await Assert.That(SearchProvider.Arr.Provider()).IsNone();
	}

	[Test]
	public async Task Provider_OfASink_IsTheServiceBehindIt()
	{
		await Assert.That(SinkKind.Plex.Provider()).IsSome().And.IsEqualTo(MediaProvider.Plex);
		await Assert.That(SinkKind.Arr.Provider()).IsNone();
	}

	[Test]
	public async Task Convert_AcrossCapabilities_FollowsTheBrand()
	{
		await Assert.That(TrendingProvider.Tmdb.AsApiKeyProvider()).IsEqualTo(ApiKeyProvider.Tmdb);
		await Assert.That(FeedProvider.Tmdb.AsApiKeyProvider()).IsEqualTo(ApiKeyProvider.Tmdb);
	}

	[Test]
	public async Task Description_OnAGeneratedEnum_MatchesTheProvider()
	{
		await Assert.That(SinkKind.Plex.GetDescription()).IsEqualTo("Plex");
		await Assert.That(TrendingProvider.Tmdb.GetDescription()).IsEqualTo("TMDB");
	}
}
