using API.Tmdb;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Shared.Options;
using Test.Shared;

namespace API.Tests.TMDB;

public class TmdbRecommendationServiceTests
{
	private const string TmdbBaseUrl = "https://api.themoviedb.org/3";

	[Test]
	public async Task GetTrendingAsync_WhenItemResolves_ReturnsResolvedTrendingMedia()
	{
		var watchlistTargetId = PlexId.From("plex-42", MediaType.Movie);
		var mediaId = MediaId.From(Guid.NewGuid());
		var json = BuildTrendingJson(id: 42, mediaType: "movie", title: "Resolved Movie");
		var media = new FakeMediaService().RegisterResolution(TmdbId.From("42", MediaType.Movie), mediaId);
		var testee = CreateTestee(json, media);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Count).IsEqualTo(1);
		await Assert.That(result.Items[0]).IsTypeOf<TrendingMedia>();
		await Assert.That(result.Items[0].Externals).Contains((IExternalMediaId)TmdbId.From("42", MediaType.Movie));
		await Assert.That(result.Items[0].Title).IsEqualTo("Resolved Movie");
	}

	[Test]
	public async Task GetTrendingAsync_WhenItemUnresolved_ReturnsUnresolvedTrendingMedia()
	{
		var json = BuildTrendingJson(id: 99, mediaType: "movie", title: "Unmatched Movie");
		var media = new FakeMediaService();
		var testee = CreateTestee(json, media);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Count).IsEqualTo(1);
		await Assert.That(result.Items[0]).IsTypeOf<UnresolvedTrendingMedia>();
		await Assert.That(result.Items[0].Externals).IsEquivalentTo(new IExternalMediaId[] { TmdbId.From("99", MediaType.Movie) });
		await Assert.That(result.Items[0].Title).IsEqualTo("Unmatched Movie");
	}

	[Test]
	public async Task GetTrendingAsync_WhenMediaTypeUnknown_ReturnsUnresolvedTrendingMedia()
	{
		var json = BuildTrendingJson(id: 7, mediaType: "person", title: "Some Person");
		var media = new FakeMediaService();
		var testee = CreateTestee(json, media);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Count).IsEqualTo(1);
		await Assert.That(result.Items[0]).IsTypeOf<UnresolvedTrendingMedia>();
		await Assert.That(result.Items[0].Title).IsEqualTo("Some Person");
	}

	[Test]
	public async Task GetTrendingAsync_WhenMultipleItems_PreservesSourceOrder()
	{
		var json = BuildTrendingJson(
			(id: 1, mediaType: "movie", title: "First"),
			(id: 2, mediaType: "movie", title: "Second"),
			(id: 3, mediaType: "movie", title: "Third")
		);
		var media = new FakeMediaService()
			.RegisterResolution(TmdbId.From("1", MediaType.Movie), MediaId.From(Guid.NewGuid()))
			.RegisterResolution(TmdbId.From("2", MediaType.Movie), MediaId.From(Guid.NewGuid()))
			.RegisterResolution(TmdbId.From("3", MediaType.Movie), MediaId.From(Guid.NewGuid()));
		var testee = CreateTestee(json, media);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Select(i => i.Title).ToList()).IsEquivalentTo(["First", "Second", "Third"]);
	}

	private static TmdbRecommendationService CreateTestee(string responseJson, FakeMediaService media)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(responseJson);
		var options = new TmdbOptions { BaseUrl = TmdbBaseUrl, ImageBaseUrl = "https://image.tmdb.org/t/p/original" };
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, Any()).Returns("test-key");
		var client = new TmdbClient(handler.CreateClient(TmdbBaseUrl), options, apiKeyStore);
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
		return new TmdbRecommendationService(client, options, media, providerConfig.Object);
	}

	private static string BuildTrendingJson(int id, string mediaType, string title) => BuildTrendingJson((id, mediaType, title));

	private static string BuildTrendingJson(params (int id, string mediaType, string title)[] items)
	{
		var results = string.Join(
			",",
			items.Select(item =>
				$$"""
				{
				  "adult": false,
				  "backdrop_path": "/backdrop.jpg",
				  "id": {{item.id}},
				  "title": "{{item.title}}",
				  "original_title": "{{item.title}}",
				  "overview": "An overview",
				  "poster_path": "/poster.jpg",
				  "media_type": "{{item.mediaType}}",
				  "original_language": "en",
				  "genre_ids": [28],
				  "popularity": 1.5,
				  "release_date": "2024-01-15",
				  "video": false,
				  "vote_average": 7.5,
				  "vote_count": 100
				}
				"""
			)
		);
		return $$"""
			{
			  "page": 1,
			  "results": [{{results}}],
			  "total_pages": 5,
			  "total_results": {{items.Length}}
			}
			""";
	}
}
