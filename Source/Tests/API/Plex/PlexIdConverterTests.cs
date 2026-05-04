using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.TMDB;
using API.TMDB.Types;
using Bogus;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;
using Shared.Options;
using Test.Shared;

namespace API.Tests.Plex;

public class PlexIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsPlexId_ReturnsPlexIdWithAdditionalIds()
	{
		var payload = CreatePlexByIdPayload(true, true);
		var searchService = IMediaSearchService.Mock();
		var testee = new PlexIdConverter(null!, CreatePlexApi(payload.Json), searchService);

		var result = await testee.ConvertAsync([payload.PlexId], new None());

		await Assert
			.That(result)
			.IsOfUnionType<ConversionResult, Resolved>()
			.And.Multiple(async resolved =>
			{
				await Assert.That(resolved.Id).IsEqualTo(payload.PlexId);
				await Assert.That(resolved.AdditionalIds.OfType<TmdbId>()).Contains(payload.TmdbId);
				await Assert.That(resolved.AdditionalIds.OfType<ImdbId>()).Contains(payload.ImdbId.GetValueOrDefault());
				await Assert.That(resolved.AdditionalIds.OfType<TvdbId>()).Contains(payload.TvdbId.GetValueOrDefault());
			});
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbAndSearchMatches_ReturnsResolvedPlexId()
	{
		var payload = CreatePlexByIdPayload(true, true);
		var searchService = IMediaSearchService.Mock();
		List<IMediaDetailResult> searchResults =
		[
			new MovieDetailResult
			{
				SourceId = payload.PlexId,
				Title = "match",
				PosterUrl = string.Empty,
				BackdropUrl = string.Empty,
				Runtime = TimeSpan.Zero,
				ReleaseDate = DateOnly.MinValue,
				Genres = [],
				AdditionalIds = [],
				Tagline = string.Empty,
				Summary = string.Empty,
			},
		];
		searchService.SearchAsync(Any(), Any(), Any()).Returns(searchResults);
		var tmdbApi = CreateTmdbApi(payload.TmdbId, "match");
		var testee = new PlexIdConverter(tmdbApi, CreatePlexApi(payload.Json), searchService);

		var result = await testee.ConvertAsync([payload.TmdbId], new None());

		await Assert
			.That(result)
			.IsOfUnionType<ConversionResult, Resolved>()
			.And.Multiple(async resolved =>
			{
				await Assert.That(resolved.Id).IsEqualTo(payload.PlexId);
				await Assert.That(resolved.AdditionalIds.OfType<TmdbId>()).Contains(payload.TmdbId);
				await Assert.That(resolved.AdditionalIds.OfType<ImdbId>()).Contains(payload.ImdbId!.Value);
				await Assert.That(resolved.AdditionalIds.OfType<TvdbId>()).Contains(payload.TvdbId!.Value);
			});
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbAndSearchHasNoMatch_ReturnsFailed()
	{
		var payload = CreatePlexByIdPayload(false, false);
		var searchService = IMediaSearchService.Mock();
		searchService.SearchAsync(Any(), Any(), Any()).Returns(new List<IMediaDetailResult>());
		var tmdbApi = CreateTmdbApi(payload.TmdbId, "match");
		var testee = new PlexIdConverter(tmdbApi, CreatePlexApi(payload.Json), searchService);

		var result = await testee.ConvertAsync([payload.TmdbId], new None());
		await Assert.That(result).IsOfUnionType<ConversionResult, Unresolved>();
	}

	private static TmdbApi CreateTmdbApi(TmdbId tmdbId, string originalTitle)
	{
		var details = new TmdbTypes.Details(int.Parse(tmdbId.Value), originalTitle, null, "2021-01-01", null, originalTitle, null, "movie");
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(JsonSerializer.Serialize(details));
		var apiKeyStore = IApiKeyConfigRepository.Mock();
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
			guids.Add(new PlexTypes.ById.Guid($"imdb://{imdbId.Value.Value}"));
		}

		if (tvdbId is not null)
		{
			guids.Add(new PlexTypes.ById.Guid($"tvdb://{tvdbId.Value.Value}"));
		}

		var metadata = new PlexTypes.ById.Metadata(
			faker.System.FilePath(),
			plexValue,
			faker.Lorem.Sentence(),
			faker.Company.CatchPhrase(),
			"movie",
			faker.Date.PastOffset().ToUnixTimeSeconds(),
			faker.Random.Double(0.0, 10.0),
			faker.Date.PastDateOnly().ToString("yyyy-MM-dd"),
			faker.System.FilePath(),
			faker.Lorem.Sentence(2),
			faker.Date.Past().Year,
			faker.Random.Int(1, 300) * 60 * 1000,
			null,
			null,
			guids,
			[new PlexTypes.ById.Genre(faker.Lorem.Word())]
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
