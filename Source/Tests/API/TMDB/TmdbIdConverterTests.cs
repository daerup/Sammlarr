using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.TMDB;
using Bogus;
using Contracts.Auth;
using Contracts.Media;

namespace API.Tests.TMDB;

public class TmdbIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbId_ReturnsSameId()
	{
		var testee = new TmdbIdConverter(null!);
		var tmdbId = TmdbId.From("100", MediaType.Movie);

		var result = await testee.ConvertAsync(tmdbId);

		await Assert.That(result.Successful).IsTrue();
		await Assert.That(result.Id).IsEqualTo(tmdbId);
		await Assert.That(result.AdditionalIds).IsEmpty();
	}

	[Test]
	public async Task ConvertAsync_WhenPlexMetadataContainsTmdbIds_ReturnsFirstTmdbAndOtherIds()
	{
		var payload = CreatePlexByIdPayload(includeTmdbId: true, includeExtraTmdbId: true, includeTvdbId: true);
		var testee = new TmdbIdConverter(CreatePlexApi(payload.Json));

		var result = await testee.ConvertAsync(payload.PlexId);

		await Assert.That(result.Successful).IsTrue();
		await Assert.That(result.Id).IsEqualTo(payload.TmdbIds[0]);
		await Assert.That(result.AdditionalIds.OfType<TmdbId>()).Contains(payload.TmdbIds[1]);
		await Assert.That(result.AdditionalIds.OfType<PlexId>()).Contains(payload.PlexId);
		await Assert.That(result.AdditionalIds.OfType<ImdbId>()).Contains(payload.ImdbId);
		await Assert.That(result.AdditionalIds.OfType<TvdbId>()).Contains(payload.TvdbId.GetValueOrDefault());
	}

	[Test]
	public async Task ConvertAsync_WhenPlexMetadataHasNoTmdbIds_ReturnsUnresolvedWithOtherIds()
	{
		var payload = CreatePlexByIdPayload(includeTmdbId: false, includeExtraTmdbId: false, includeTvdbId: false);
		var testee = new TmdbIdConverter(CreatePlexApi(payload.Json));

		var result = await testee.ConvertAsync(payload.PlexId);

		await Assert.That(result.Successful).IsFalse();
		await Assert.That(result.AllIds.OfType<PlexId>()).Contains(payload.PlexId);
		await Assert.That(result.AllIds.OfType<ImdbId>()).Contains(payload.ImdbId);
		await Assert.That(result.AllIds.OfType<TmdbId>()).IsEmpty();
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsImdb_ThrowsNotSupportedException()
	{
		var testee = new TmdbIdConverter(null!);

		await Assert.That(async () => await testee.ConvertAsync(ImdbId.From("tt123", MediaType.Movie))).Throws<NotSupportedException>();
	}

	private static PlexByIdPayload CreatePlexByIdPayload(bool includeTmdbId, bool includeExtraTmdbId, bool includeTvdbId)
	{
		var faker = new Faker();
		var plexValue = $"plex-{faker.Random.AlphaNumeric(10)}";
		var imdbId = ImdbId.From($"tt{faker.Random.Int(100_000, 999_999)}", MediaType.Movie);
		TvdbId? tvdbId = includeTvdbId ? TvdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie) : null;
		var tmdbIds = new List<TmdbId>();
		if (includeTmdbId)
		{
			tmdbIds.Add(TmdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie));
		}
		if (includeExtraTmdbId)
		{
			tmdbIds.Add(TmdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie));
		}

		List<PlexTypes.ById.Guid> guids = [new($"imdb://{imdbId.Value}")];
		foreach (var tmdbId in tmdbIds)
		{
			guids.Add(new($"tmdb://{tmdbId.Value}"));
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

		return new PlexByIdPayload(JsonSerializer.Serialize(root), PlexId.From(plexValue, MediaType.Movie), tmdbIds, imdbId, tvdbId);
	}

	private static PlexApi CreatePlexApi(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var credential = new StoredCredential(WatchlistProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, CancellationToken.None).Returns(credential);
		var plexClient = new PlexClient(handler.CreateClient(PlexBaseUrl), credentialStore);
		return new PlexApi(plexClient);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, IReadOnlyList<TmdbId> TmdbIds, ImdbId ImdbId, TvdbId? TvdbId);
}
