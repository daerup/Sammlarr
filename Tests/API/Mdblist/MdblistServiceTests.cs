using System.Net;
using API.Mdblist;
using Contracts;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Domain.Auth;
using Domain.Providers;
using Domain.Settings;
using Shared.Options;
using TestInfrastructure;

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
		IApiKey apiKey = new TestApiKey(ApiKeyProvider.Mdblist, "test-key");
		apiKeyStore.GetApiKeyAsync(ApiKeyProvider.Mdblist, Any()).Returns(apiKey);
		var options = new MdblistOptions { BaseUrl = BaseUrl };
		var client = new MdblistClient(handler.CreateClient(BaseUrl), options);
		var media = IMediaService.Mock();
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
		var testee = new MdblistService(client, media.Object, providerConfig.Object, apiKeyStore.Object);

		var act = async () =>
		{
			await foreach (var _ in testee.GetFeedItemsAsync(identifier)) { }
		};

		var exception = await Assert.That(act).Throws<FeedIdentifierNotFoundException>();
		await Assert.That(exception!.Identifier).IsEqualTo(identifier);
	}
}
