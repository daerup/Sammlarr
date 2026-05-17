using API.TMDB;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Shared.Options;

namespace API.Tests.TMDB;

public class TmdbRecommendationServiceTests
{
	private const string TmdbBaseUrl = "https://api.themoviedb.org/3";

	[Test]
	public async Task GetTrendingAsync_WhenItemResolves_ReturnsResolvedTrendingMedia()
	{
		var watchlistTargetId = PlexId.From("plex-42", MediaType.Movie);
		var json = BuildTrendingJson(id: 42, mediaType: "movie", title: "Resolved Movie");
		var resolver = IMediaIdResolver.Mock();
		resolver
			.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any<Optional<ResolutionHints>>(), Any())
			.Returns(new Resolved(watchlistTargetId, []));
		var testee = CreateTestee(json, resolver);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Count).IsEqualTo(1);
		await Assert.That(result.Items[0]).IsTypeOf<TrendingMedia>();
		await Assert.That(result.Items[0].Ids).Contains((IMediaId)watchlistTargetId);
		await Assert.That(result.Items[0].Title).IsEqualTo("Resolved Movie");
	}

	[Test]
	public async Task GetTrendingAsync_WhenItemResolves_IncludesSourceAndResolvedIds()
	{
		var sourceTmdbId = TmdbId.From("42", MediaType.Movie);
		var plexId = PlexId.From("plex-42", MediaType.Movie);
		var imdbId = ImdbId.From("tt0042", MediaType.Movie);
		var json = BuildTrendingJson(id: 42, mediaType: "movie", title: "Resolved Movie");
		var resolver = IMediaIdResolver.Mock();
		resolver
			.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any<Optional<ResolutionHints>>(), Any())
			.Returns(new Resolved(plexId, [imdbId]));
		var testee = CreateTestee(json, resolver);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items[0].Ids).IsEquivalentTo(new IMediaId[] { sourceTmdbId, plexId, imdbId });
	}

	[Test]
	public async Task GetTrendingAsync_WhenResolvedAdditionalIdsContainSource_DedupesIds()
	{
		var sourceTmdbId = TmdbId.From("42", MediaType.Movie);
		var plexId = PlexId.From("plex-42", MediaType.Movie);
		var json = BuildTrendingJson(id: 42, mediaType: "movie", title: "Resolved Movie");
		var resolver = IMediaIdResolver.Mock();
		resolver
			.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any<Optional<ResolutionHints>>(), Any())
			.Returns(new Resolved(plexId, [sourceTmdbId]));
		var testee = CreateTestee(json, resolver);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items[0].Ids).IsEquivalentTo(new IMediaId[] { sourceTmdbId, plexId });
	}

	[Test]
	public async Task GetTrendingAsync_WhenItemUnresolved_ReturnsUnresolvedTrendingMedia()
	{
		var json = BuildTrendingJson(id: 99, mediaType: "movie", title: "Unmatched Movie");
		var resolver = IMediaIdResolver.Mock();
		resolver.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any<Optional<ResolutionHints>>(), Any()).Returns(new Unresolved());
		var testee = CreateTestee(json, resolver);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Count).IsEqualTo(1);
		await Assert.That(result.Items[0]).IsTypeOf<UnresolvedTrendingMedia>();
		await Assert.That(result.Items[0].Ids).IsEquivalentTo(new IMediaId[] { TmdbId.From("99", MediaType.Movie) });
		await Assert.That(result.Items[0].Title).IsEqualTo("Unmatched Movie");
	}

	[Test]
	public async Task GetTrendingAsync_WhenMediaTypeUnknown_DropsItem()
	{
		var json = BuildTrendingJson(id: 7, mediaType: "person", title: "Some Person");
		var resolver = IMediaIdResolver.Mock();
		var testee = CreateTestee(json, resolver);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items).IsEmpty();
	}

	[Test]
	public async Task GetTrendingAsync_WhenMultipleItems_PreservesSourceOrder()
	{
		var json = BuildTrendingJson(
			(id: 1, mediaType: "movie", title: "First"),
			(id: 2, mediaType: "movie", title: "Second"),
			(id: 3, mediaType: "movie", title: "Third")
		);
		var resolver = IMediaIdResolver.Mock();
		resolver
			.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any<Optional<ResolutionHints>>(), Any())
			.Returns(new Resolved(PlexId.From("plex-1", MediaType.Movie), []));
		var testee = CreateTestee(json, resolver);

		var result = await testee.GetTrendingAsync(page: 1);

		await Assert.That(result.Items.Select(i => i.Title).ToList()).IsEquivalentTo(["First", "Second", "Third"]);
	}

	private static TmdbRecommendationService CreateTestee(string responseJson, Mock<IMediaIdResolver> resolver)
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
		return new TmdbRecommendationService(client, options, resolver.Object, providerConfig.Object);
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
