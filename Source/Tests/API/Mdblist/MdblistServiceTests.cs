using System.Net;
using API.Mdblist;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Feed;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Shared.Options;

namespace API.Tests.Mdblist;

public class MdblistServiceTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task GetFeedItemsAsync_WhenIdentifierReturns404_ThrowsFeedIdentifierNotFound()
	{
		const string identifier = "missing-list";
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Mdblist, Any()).Returns("test-key");
		var options = new MdblistOptions { BaseUrl = BaseUrl };
		var client = new MdblistClient(handler.CreateClient(BaseUrl), options, apiKeyStore);
		var resolver = IMediaIdResolver.Mock();
		var metadataProvider = IMediaMetadataProvider.Mock();
		var providerConfig = IProviderConfigRepository.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
				}
			);
		var testee = new MdblistService(client, resolver.Object, metadataProvider.Object, providerConfig.Object);

		var act = async () =>
		{
			await foreach (var _ in testee.GetFeedItemsAsync(identifier)) { }
		};

		var exception = await Assert.That(act).Throws<FeedIdentifierNotFoundException>();
		await Assert.That(exception!.Identifier).IsEqualTo(identifier);
	}
}
