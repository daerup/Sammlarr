using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.TMDB;
using Bogus;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Test.Shared;

namespace API.Tests.TMDB;

public class TmdbIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbId_ReturnsSameId()
	{
		var testee = new TmdbIdConverter(null!);
		var tmdbId = TmdbId.From("100", MediaType.Movie);

		var result = await testee.ConvertAsync([tmdbId], new None());

		using var _ = Assert.Multiple();
		await Assert.That(result.Value).IsTypeOf<Some<IReadOnlyList<IExternalMediaId>>>();
		var externals = ((Some<IReadOnlyList<IExternalMediaId>>)result.Value).Value;
		await Assert.That(externals).Contains((IExternalMediaId)tmdbId);
	}

	[Test]
	public async Task ConvertAsync_WhenPlexMetadataContainsTmdbIds_ReturnsFirstTmdbAndOtherIds()
	{
		var payload = CreatePlexByIdPayload(true, true, true);
		var testee = new TmdbIdConverter(CreatePlexApi(payload.Json));

		var result = await testee.ConvertAsync([payload.PlexId], new None());

		using var _ = Assert.Multiple();
		await Assert.That(result.Value).IsTypeOf<Some<IReadOnlyList<IExternalMediaId>>>();
		var externals = ((Some<IReadOnlyList<IExternalMediaId>>)result.Value).Value;
		await Assert.That(externals).Contains((IExternalMediaId)payload.TmdbIds[0]);
		await Assert.That(externals).Contains((IExternalMediaId)payload.TmdbIds[1]);
		await Assert.That(externals).Contains((IExternalMediaId)payload.PlexId);
		await Assert.That(externals).Contains((IExternalMediaId)payload.ImdbId);
		await Assert.That(externals).Contains((IExternalMediaId)payload.TvdbId!.Value);
	}

	[Test]
	public async Task ConvertAsync_WhenPlexMetadataHasNoTmdbIds_ReturnsUnresolvedWithOtherIds()
	{
		var payload = CreatePlexByIdPayload(false, false, false);
		var testee = new TmdbIdConverter(CreatePlexApi(payload.Json));

		var result = await testee.ConvertAsync([payload.PlexId], new None());

		await Assert.That(result.Value).IsTypeOf<None>();
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
			guids.Add(new PlexTypes.ById.Guid($"tmdb://{tmdbId.Value}"));
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
		var mediaRepository = IMediaRepository.Mock();
		mediaRepository
			.UpsertAsync(Any<IEnumerable<IExternalMediaId>>(), Any())
			.Returns(new MediaIdentity(MediaId.From(Guid.NewGuid()), new List<IExternalMediaId>()));
		return new PlexApi(plexClient, mediaRepository.Object);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, IReadOnlyList<TmdbId> TmdbIds, ImdbId ImdbId, TvdbId? TvdbId);
}
