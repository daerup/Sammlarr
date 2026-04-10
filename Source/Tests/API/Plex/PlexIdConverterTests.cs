using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.TMDB;
using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Search;
using Shared.Options;

namespace API.Tests.Plex;

public class PlexIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsImdb_ThrowsNotSupportedException()
	{
		var searchService = IMediaSearchService.Mock();
		var testee = new PlexIdConverter(null!, null!, searchService);

		await Assert.That(async () => await testee.ConvertAsync(ImdbId.From("tt123", MediaType.Movie))).Throws<NotSupportedException>();
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsPlexId_ReturnsPlexIdWithAdditionalIds()
	{
		var payload = CreatePlexByIdPayload(includeImdbId: true, includeTvdbId: true);
		var searchService = IMediaSearchService.Mock();
		var testee = new PlexIdConverter(null!, CreatePlexApi(payload.Json), searchService);

		var result = await testee.ConvertAsync(payload.PlexId);

		await Assert.That(result.Successful).IsTrue();
		await Assert.That(result.Id).IsEqualTo(payload.PlexId);
		await Assert.That(result.AdditionalIds.OfType<TmdbId>()).Contains(payload.TmdbId);
		await Assert.That(result.AdditionalIds.OfType<ImdbId>()).Contains(payload.ImdbId.GetValueOrDefault());
		await Assert.That(result.AdditionalIds.OfType<TvdbId>()).Contains(payload.TvdbId.GetValueOrDefault());
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbAndSearchMatches_ReturnsResolvedPlexId()
	{
		var payload = CreatePlexByIdPayload(includeImdbId: true, includeTvdbId: true);
		var searchService = IMediaSearchService.Mock();
		List<IMediaSearchResult> searchResults =
		[
			new MovieSearchResult
			{
				SourceId = payload.PlexId,
				Title = "match",
				Year = 2024,
				PosterUrl = null,
				BackdropUrl = null,
				Runtime = null,
				ReleaseDate = null,
				Genres = [],
			},
		];
		searchService.SearchAsync(Any(), Any(), Any()).Returns(searchResults);
		var tmdbApi = CreateTmdbApi(payload.TmdbId, "match");
		var testee = new PlexIdConverter(tmdbApi, CreatePlexApi(payload.Json), searchService);

		var result = await testee.ConvertAsync(payload.TmdbId);

		await Assert.That(result.Successful).IsTrue();
		await Assert.That(result.Id).IsEqualTo(payload.PlexId);
		await Assert.That(result.AdditionalIds.OfType<TmdbId>()).Contains(payload.TmdbId);
		await Assert.That(result.AdditionalIds.OfType<ImdbId>()).Contains(payload.ImdbId.GetValueOrDefault());
		await Assert.That(result.AdditionalIds.OfType<TvdbId>()).Contains(payload.TvdbId.GetValueOrDefault());
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbAndSearchHasNoMatch_ReturnsFailed()
	{
		var payload = CreatePlexByIdPayload(includeImdbId: false, includeTvdbId: false);
		var searchService = IMediaSearchService.Mock();
		searchService.SearchAsync(Any(), Any(), Any()).Returns(new List<IMediaSearchResult>());
		var tmdbApi = CreateTmdbApi(payload.TmdbId, "match");
		var testee = new PlexIdConverter(tmdbApi, CreatePlexApi(payload.Json), searchService);

		var result = await testee.ConvertAsync(payload.TmdbId);

		await Assert.That(result.Successful).IsFalse();
		await Assert.That(result.AllIds).IsEmpty();
	}

	private static TmdbApi CreateTmdbApi(TmdbId tmdbId, string originalTitle)
	{
		var details = new API.TMDB.Types.TmdbTypes.Details(
			Id: int.Parse(tmdbId.Value),
			OriginalTitle: originalTitle,
			OriginalName: null,
			ReleaseDate: "2021-01-01",
			FirstAirDate: null,
			Title: originalTitle,
			Name: null,
			Type: "movie"
		);
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(JsonSerializer.Serialize(details));
		var apiKeyStore = IApiKeyStore.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, Any()).Returns("test-api-key");
		var options = new TmdbOptions { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p/original" };
		var tmdbClient = new TmdbClient(handler.CreateClient(options.BaseUrl), options, apiKeyStore);
		return new TmdbApi(tmdbClient);
	}

	private static PlexByIdPayload CreatePlexByIdPayload(bool includeImdbId, bool includeTvdbId)
	{
		var faker = new Faker();
		var plexValue = $"plex-{faker.Random.AlphaNumeric(10)}";
		var tmdbId = TmdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie);
		ImdbId? imdbId = includeImdbId ? ImdbId.From($"tt{faker.Random.Int(100_000, 999_999)}", MediaType.Movie) : null;
		TvdbId? tvdbId = includeTvdbId ? TvdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie) : null;

		List<PlexTypes.ById.Guid> guids = [new($"tmdb://{tmdbId.Value}")];
		if (imdbId is not null)
		{
			guids.Add(new($"imdb://{imdbId.Value.Value}"));
		}

		if (tvdbId is not null)
		{
			guids.Add(new($"tvdb://{tvdbId.Value.Value}"));
		}

		var metadata = new PlexTypes.ById.Metadata(
			Art: faker.System.FilePath(),
			RatingKey: plexValue,
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

		return new PlexByIdPayload(JsonSerializer.Serialize(root), PlexId.From(plexValue, MediaType.Movie), tmdbId, imdbId, tvdbId);
	}

	private static PlexApi CreatePlexApi(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = new StoredCredential(WatchlistProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(httpClient, credentialStore);
		return new PlexApi(plexClient);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, TmdbId TmdbId, ImdbId? ImdbId, TvdbId? TvdbId);
}
