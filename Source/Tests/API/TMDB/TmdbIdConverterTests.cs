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
using Imposter.Abstractions;
using Shared.Options;
using ImposterArg = Imposter.Abstractions.Arg;
using MediaIdMappingStoreMock = TUnit.Mocks.Generated.Contracts.Media.IMediaIdMappingStoreMock;
using MediaSearchServiceMock = TUnit.Mocks.Generated.Contracts.Media.Search.IMediaSearchServiceMock;

namespace API.Tests.TMDB;

public class TmdbIdConverterTests
{
	private const string TmdbBaseUrl = "https://api.themoviedb.org/3";
	private const string TmdbImageBaseUrl = "https://image.tmdb.org/t/p/original";
	private const string PlexBaseUrl = "https://plex.tv";
	private readonly Faker<MovieSearchResult> _movieSearchResultFaker = new Faker<MovieSearchResult>()
		.RuleFor(x => x.Title, f => f.Lorem.Sentence(2))
		.RuleFor(x => x.Year, f => f.Date.Past().Year)
		.RuleFor(x => x.PosterUrl, f => f.Internet.Url())
		.RuleFor(x => x.BackdropUrl, f => f.Internet.Url())
		.RuleFor(x => x.Runtime, (f, _) => TimeSpan.FromMinutes(f.Random.Int(60, 240)))
		.RuleFor(x => x.ReleaseDate, f => f.Date.PastDateOnly())
		.RuleFor(x => x.Genres, f => [f.Music.Genre(), f.Music.Genre()]);

	private MediaIdMappingStoreMock store = null!;
	private MediaSearchServiceMock searchService = null!;
	private IKeyedServiceIndexImposter<MediaProvider, IMediaSearchService> searchServiceIndex = null!;

	[Before(Test)]
	public void BeforeTest()
	{
		store = IMediaIdMappingStore.Mock();
		searchService = IMediaSearchService.Mock();
		var keyedServiceIndex = IKeyedServiceIndex<MediaProvider, IMediaSearchService>.Imposter<MediaProvider, IMediaSearchService>();
		keyedServiceIndex[ImposterArg.Any].Getter().Returns(searchService);
		searchServiceIndex = keyedServiceIndex;
	}

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
		store.GetByIdAsync(TmdbId.From(100), CancellationToken.None).Returns(cachedIds);
		var testee = new TmdbIdConverter(null!, null!, store, null!);

		var result = await testee.ConvertAsync(TmdbId.From(100), MediaType.Movie);

		await Assert.That(result).IsEquivalentTo(cachedIds);
		store.GetByIdAsync(TmdbId.From(100), CancellationToken.None).WasCalled(Times.Once);
		store.VerifyNoOtherCalls();
	}

	[Test]
	public async Task ConvertAsync_WhenSearchReturnsNoResults_ReturnsEmptyList()
	{
		var (tmdb, plex) = CreatePayloads(includeTvdbId: true);
		store.GetByIdAsync(tmdb.Id, Any()).Returns([]);
		searchService.SearchAsync(tmdb.OriginalTitle, Any(), Any()).Returns([]);

		var testee = new TmdbIdConverter(tmdb.Api, plex.Api, store, searchServiceIndex.Instance());

		var result = await testee.ConvertAsync(tmdb.Id, MediaType.Movie);

		await Assert.That(result).IsEmpty();
		store.GetByIdAsync(tmdb.Id, Any()).WasCalled(Times.Once);
		store.VerifyNoOtherCalls();
		searchService.SearchAsync(tmdb.OriginalTitle, Any(), Any()).WasCalled(Times.Once);
		searchService.VerifyNoOtherCalls();
	}

	[Test]
	public async Task ConvertAsync_WhenMatchFoundWithTvdb_ReturnsNonTmdbIds()
	{
		var (tmdb, plex) = CreatePayloads(includeTvdbId: true);
		store.GetByIdAsync(tmdb.Id, Any()).Returns([]);
		List<IMediaSearchResult> searchResults = [_movieSearchResultFaker.RuleFor(x => x.SourceId, plex.Id).Generate()];
		searchService.SearchAsync(tmdb.OriginalTitle, Any(), Any()).Returns(searchResults);

		var testee = new TmdbIdConverter(tmdb.Api, plex.Api, store, searchServiceIndex.Instance());

		var result = await testee.ConvertAsync(tmdb.Id, MediaType.Movie);

		await Assert.That(result).Count().IsEqualTo(3);
		await Assert.That(result.OfType<PlexId>().Single()).IsEqualTo(plex.Id);
		await Assert.That(result.OfType<ImdbId>().Single()).IsEqualTo(plex.ImdbId);
		await Assert.That(result.OfType<TvdbId>().Single()).IsEqualTo(plex.TvdbId!.Value);
		await Assert.That(result.OfType<TmdbId>()).IsEmpty();
		store.GetByIdAsync(tmdb.Id, CancellationToken.None).WasCalled(Times.Once);
		searchService.SearchAsync(tmdb.OriginalTitle, Any(), Any()).WasCalled(Times.Once);
		searchService.VerifyNoOtherCalls();
	}

	[Test]
	public async Task ConvertAsync_WhenMatchFound_ReturnsNonTmdbIds()
	{
		var (tmdb, plex) = CreatePayloads(includeTvdbId: false);
		var existingCacheIds = new List<IMediaId> { tmdb.Id };
		store.GetByIdAsync(tmdb.Id, CancellationToken.None).Returns(existingCacheIds);
		IReadOnlyList<IMediaSearchResult> searchResults = [_movieSearchResultFaker.RuleFor(x => x.SourceId, plex.Id).Generate()];
		searchService.SearchAsync(tmdb.OriginalTitle, Any(), Any()).Returns(searchResults);

		var testee = new TmdbIdConverter(tmdb.Api, plex.Api, store, searchServiceIndex.Instance());

		await testee.ConvertAsync(tmdb.Id, MediaType.Movie);

		store.GetByIdAsync(tmdb.Id, CancellationToken.None).WasCalled(Times.Once);
		searchService.SearchAsync(tmdb.OriginalTitle, Any(), Any()).WasCalled(Times.Once);
		store.VerifyNoOtherCalls();
		searchService.VerifyNoOtherCalls();
	}

	private static ((TmdbId Id, string OriginalTitle, TmdbApi Api) Tmdb, (PlexId Id, ImdbId ImdbId, TvdbId? TvdbId, PlexApi Api) Plex) CreatePayloads(
		bool includeTvdbId
	)
	{
		var (tmdbJson, tmdbId, originalTitle) = CreateTmdbDetailsPayload();
		var (plexJson, plexId, imdbId, tvdbId) = CreatePlexByIdPayload(tmdbId, includeTvdbId);
		return ((tmdbId, originalTitle, CreateTmdbApi(tmdbJson)), (plexId, imdbId, tvdbId, CreatePlexApi(plexJson)));
	}

	private static (string Json, TmdbId TmdbId, string OriginalTitle) CreateTmdbDetailsPayload()
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

		return (JsonSerializer.Serialize(details), tmdbId, title);
	}

	private static (string Json, PlexId PlexId, ImdbId ImdbId, TvdbId? TvdbId) CreatePlexByIdPayload(TmdbId tmdbId, bool includeTvdbId)
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

		return (JsonSerializer.Serialize(root), plexId, imdbId, tvdbId);
	}

	private static TmdbApi CreateTmdbApi(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var options = new TmdbOptions { BaseUrl = TmdbBaseUrl, ImageBaseUrl = TmdbImageBaseUrl };
		var apiKeyStore = IApiKeyStore.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, CancellationToken.None).Returns("test-api-key");
		var tmdbClient = new TmdbClient(handler.CreateClient(options.BaseUrl), options, apiKeyStore);
		return new TmdbApi(tmdbClient, options, null!);
	}

	private static PlexApi CreatePlexApi(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var credential = new StoredCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, CancellationToken.None).Returns(credential);
		var plexClient = new PlexClient(handler.CreateClient(PlexBaseUrl), credentialStore);
		return new PlexApi(plexClient);
	}
}
