using System.Net;
using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.TMDB;
using API.TMDB.Types;
using Bogus;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Shared.Options;

namespace API.Tests.TMDB;

public class TmdbIdConverterTests
{
	[Test]
	public async Task ConvertAsync_WhenMediaIdIsNotTmdbId_ReturnsEmptyList()
	{
		var testee = new TmdbIdConverter(null!, null!, null!, null!);

		var result = await testee.ConvertAsync(PlexId.From("plex-1"), MediaType.Movie);

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task ConvertAsync_WhenCacheHasPlexId_ReturnsCachedIds()
	{
		var cachedIds = new List<IMediaId> { TmdbId.From(100), PlexId.From("plex-1") };
		var store = IMediaIdMappingStore.Mock();
		store.GetByIdAsync(Any(), Any()).Returns(cachedIds);
		var testee = new TmdbIdConverter(null!, null!, store, null!);

		var result = await testee.ConvertAsync(TmdbId.From(100), MediaType.Movie);

		await Assert.That(result).IsEquivalentTo(cachedIds);
	}

	[Test]
	public async Task ConvertAsync_WhenSearchReturnsNoResults_ReturnsEmptyList()
	{
		var tmdbPayload = CreateTmdbDetailsPayload();
		var store = IMediaIdMappingStore.Mock();
		store.GetByIdAsync(Any(), Any()).Returns([]);
		var searchService = IMediaSearchService.Mock();
		searchService.SearchAsync(Any(), Any(), Any()).Returns([]);
		var testee = new TmdbIdConverter(CreateTmdbApi(new FakeJsonHandler(tmdbPayload.Json)), null!, store, new TestSearchIndex(searchService));

		var result = await testee.ConvertAsync(tmdbPayload.TmdbId, MediaType.Movie);

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task ConvertAsync_WhenMatchFound_ReturnsNonTmdbIds()
	{
		var tmdbPayload = CreateTmdbDetailsPayload();
		var plexPayload = CreatePlexByIdPayload(tmdbPayload.TmdbId, includeTvdbId: true);
		var store = IMediaIdMappingStore.Mock();
		store.GetByIdAsync(Any(), Any()).Returns([]);
		var searchService = IMediaSearchService.Mock();
		IReadOnlyList<IMediaSearchResult> searchResults = [CreateMovieSearchResult(plexPayload.PlexId)];
		searchService.SearchAsync(Any(), Any(), Any()).Returns(searchResults);
		var testee = new TmdbIdConverter(
			CreateTmdbApi(new FakeJsonHandler(tmdbPayload.Json)),
			CreatePlexApi(new FakeJsonHandler(plexPayload.Json)),
			store,
			new TestSearchIndex(searchService)
		);

		var result = await testee.ConvertAsync(tmdbPayload.TmdbId, MediaType.Movie);

		await Assert.That(result).Count().IsEqualTo(3);
		await Assert.That(result.OfType<PlexId>().Single()).IsEqualTo(plexPayload.PlexId);
		await Assert.That(result.OfType<ImdbId>().Single()).IsEqualTo(plexPayload.ImdbId);
		await Assert.That(result.OfType<TvdbId>().Single()).IsEqualTo(plexPayload.TvdbId.GetValueOrDefault());
		await Assert.That(result.OfType<TmdbId>()).IsEmpty();
	}

	[Test]
	public async Task ConvertAsync_WhenMatchFoundAndCacheHasNonPlexIds_AppendsAndReturnsNonTmdbIds()
	{
		var tmdbPayload = CreateTmdbDetailsPayload();
		var plexPayload = CreatePlexByIdPayload(tmdbPayload.TmdbId, includeTvdbId: false);
		var existingCacheIds = new List<IMediaId> { tmdbPayload.TmdbId };
		var store = IMediaIdMappingStore.Mock();
		store.GetByIdAsync(Any(), Any()).Returns(existingCacheIds);
		var searchService = IMediaSearchService.Mock();
		IReadOnlyList<IMediaSearchResult> searchResults = [CreateMovieSearchResult(plexPayload.PlexId)];
		searchService.SearchAsync(Any(), Any(), Any()).Returns(searchResults);
		var testee = new TmdbIdConverter(
			CreateTmdbApi(new FakeJsonHandler(tmdbPayload.Json)),
			CreatePlexApi(new FakeJsonHandler(plexPayload.Json)),
			store,
			new TestSearchIndex(searchService)
		);

		await testee.ConvertAsync(tmdbPayload.TmdbId, MediaType.Movie);

		store.AppendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
	}

	private static TmdbDetailsPayload CreateTmdbDetailsPayload()
	{
		var faker = new Faker();
		var tmdbId = TmdbId.From(faker.Random.Int(1, 1_000_000));
		var title = faker.Lorem.Sentence(2);
		var details = new TmdbTypes.Details(
			Id: tmdbId.Value,
			OriginalTitle: title,
			OriginalName: null,
			ReleaseDate: faker.Date.PastDateOnly().ToString("yyyy-MM-dd"),
			FirstAirDate: null,
			Title: title,
			Name: null
		);

		return new TmdbDetailsPayload(JsonSerializer.Serialize(details), tmdbId);
	}

	private static PlexByIdPayload CreatePlexByIdPayload(TmdbId tmdbId, bool includeTvdbId)
	{
		var faker = new Faker();
		var plexId = PlexId.From($"plex-{faker.Random.AlphaNumeric(10)}");
		var imdbId = ImdbId.From($"tt{faker.Random.Int(100_000, 999_999)}");
		TvdbId? tvdbId = includeTvdbId ? TvdbId.From(faker.Random.Int(1, 1_000_000)) : null;

		List<PlexTypes.ById.Guid> guids = [new($"tmdb://{tmdbId}"), new($"imdb://{imdbId}")];
		if (tvdbId is not null)
		{
			guids.Add(new($"tvdb://{tvdbId.Value}"));
		}

		var metadata = new PlexTypes.ById.Metadata(
			Art: faker.System.FilePath(),
			RatingKey: plexId.Value,
			Summary: faker.Lorem.Sentence(),
			Tagline: faker.Company.CatchPhrase(),
			Type: "movie",
			AddedAt: faker.Date.PastOffset().ToUnixTimeSeconds(),
			AudienceRating: faker.Random.Double(0.0, 10.0),
			OriginallyAvailableAt: faker.Date.PastDateOnly().ToString("yyyy-MM-dd"),
			Poster: faker.System.FilePath(),
			Title: faker.Lorem.Sentence(2),
			Year: faker.Date.Past().Year,
			Guid: guids
		);
		var root = new PlexTypes.ById.Root(new PlexTypes.ById.MediaContainer("1", "Movies", 0, 1, [metadata]));

		return new PlexByIdPayload(JsonSerializer.Serialize(root), plexId, imdbId, tvdbId);
	}

	private static TmdbApi CreateTmdbApi(HttpMessageHandler httpHandler)
	{
		var options = new TmdbOptions { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p/original" };
		var apiKeyStore = IApiKeyStore.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, Any()).Returns("test-api-key");
		var tmdbClient = new TmdbClient(new HttpClient(httpHandler), options, apiKeyStore);
		return new TmdbApi(tmdbClient, options, null!);
	}

	private static PlexApi CreatePlexApi(HttpMessageHandler httpHandler)
	{
		var credential = new StoredCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(new HttpClient(httpHandler), credentialStore);
		return new PlexApi(plexClient);
	}

	private static MovieSearchResult CreateMovieSearchResult(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			Title = "Test Movie",
			Year = 2021,
			PosterUrl = null,
			BackdropUrl = null,
			Runtime = null,
			ReleaseDate = null,
			Genres = [],
		};

	private sealed class TestSearchIndex(IMediaSearchService searchService) : IKeyedServiceIndex<MediaProvider, IMediaSearchService>
	{
		public IMediaSearchService this[MediaProvider key] => searchService;
	}

	private sealed class FakeJsonHandler(string json) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
	}

	private record TmdbDetailsPayload(string Json, TmdbId TmdbId);

	private record PlexByIdPayload(string Json, PlexId PlexId, ImdbId ImdbId, TvdbId? TvdbId);
}
