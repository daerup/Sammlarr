using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using Bogus;
using Contracts.Auth;
using Contracts.Media;

namespace API.Tests.Plex;

public class PlexIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsNotPlexId_ReturnsEmptyList()
	{
		var testee = new PlexIdConverter(null!, null!);

		var result = await testee.ConvertAsync(TmdbId.From("1", MediaType.Movie), MediaType.Movie);

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task ConvertAsync_WhenCacheHasResults_ReturnsCachedIds()
	{
		var expectedIds = new List<IMediaId> { TmdbId.From("1", MediaType.Movie), ImdbId.From("tt123", MediaType.Movie) };
		var store = IMediaIdGroupStore.Mock();
		store.GetByIdAsync(Any(), Any()).Returns(expectedIds);
		var testee = new PlexIdConverter(null!, store);

		var result = await testee.ConvertAsync(PlexId.From("plex-1", MediaType.Movie), MediaType.Movie);

		await Assert.That(result).IsEquivalentTo(expectedIds);
	}

	[Test]
	public async Task ConvertAsync_WhenCacheIsEmpty_FetchesExternalIdsFromApi()
	{
		var payload = CreatePlexByIdPayload(includeImdbId: true, includeTvdbId: true);
		var store = IMediaIdGroupStore.Mock();
		store.GetByIdAsync(payload.PlexId, Any()).Returns([]);
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json), store);

		var result = await testee.ConvertAsync(payload.PlexId, MediaType.Movie);

		await Assert.That(result).Count().IsEqualTo(3);
		await Assert.That(result.OfType<TmdbId>().Single()).IsEqualTo(payload.TmdbId);
		await Assert.That(result.OfType<ImdbId>().Single()).IsEqualTo(payload.ImdbId.GetValueOrDefault());
		await Assert.That(result.OfType<TvdbId>().Single()).IsEqualTo(payload.TvdbId.GetValueOrDefault());
	}

	[Test]
	public async Task ConvertAsync_WhenCacheIsEmpty_StoresMappingsIncludingPlexId()
	{
		var payload = CreatePlexByIdPayload(includeImdbId: false, includeTvdbId: false);
		var store = IMediaIdGroupStore.Mock();
		store.GetByIdAsync(payload.PlexId, Any()).Returns([]);
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json), store);

		await testee.ConvertAsync(payload.PlexId, MediaType.Movie);

		store.CreateOrAppendAsync(Is<IEnumerable<IMediaId>>(x => x!.Contains(payload.PlexId)), Any()).WasCalled(Times.Once);
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
		var credential = new StoredCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(httpClient, credentialStore);
		return new PlexApi(plexClient);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, TmdbId TmdbId, ImdbId? ImdbId, TvdbId? TvdbId);
}
