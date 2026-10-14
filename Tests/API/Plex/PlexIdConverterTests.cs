using System.Net;
using System.Text.Json;
using API.Http.Lanes;
using API.Plex;
using API.Plex.Types;
using API.Tests.Feeds;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	private const string FightClubMatch =
		"""{"MediaContainer":{"librarySectionID":"home","librarySectionTitle":"Discover","offset":0,"totalSize":1,"identifier":"tv.plex.provider.discover","size":1,"Metadata":[{"guid":"plex://movie/5d776b59ad5437001f79c6f8","ratingKey":"5d776b59ad5437001f79c6f8","type":"movie","title":"Fight Club","year":1999,"Guid":[{"id":"imdb://tt0137523"},{"id":"tmdb://550"},{"id":"tvdb://247"}]}]}}""";

	private static readonly TmdbId FightClub = TmdbId.From("550", MediaType.Movie);

	[Test]
	public async Task LookUpAsync_ForAPlexIdWithTwoTmdbGuids_ReturnsBothAndTheOtherGuids()
	{
		var payload = CreatePlexByIdPayload(true, true, true);
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json));

		var result = await testee.LookUpAsync(testee.RequestById(payload.PlexId));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).Contains(payload.TmdbIds[0]);
				await Assert.That(found.Ids).Contains(payload.TmdbIds[1]);
				await Assert.That(found.Ids).Contains(payload.PlexId);
				await Assert.That(found.Ids).Contains(payload.ImdbId);
				await Assert.That(found.Ids).Contains(payload.TvdbId!.Value);
			});
	}

	[Test]
	public async Task LookUpAsync_ForAPlexIdWithoutATmdbGuid_StillReturnsTheOtherGuids()
	{
		var payload = CreatePlexByIdPayload(false, false, false);
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json));

		var result = await testee.LookUpAsync(testee.RequestById(payload.PlexId));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).Contains(payload.PlexId);
				await Assert.That(found.Ids).Contains(payload.ImdbId);
				await Assert.That(found.Ids.Any(id => id.Source == MediaProvider.Tmdb)).IsFalse();
			});
	}

	[Test]
	public async Task LookUpAsync_ByTmdbId_AsksTheMatchEndpoint()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(FightClubMatch);
		var testee = new PlexIdConverter(CreatePlexApi(handler));

		var result = await testee.LookUpAsync(testee.RequestById(FightClub));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).Contains(PlexId.From("5d776b59ad5437001f79c6f8", MediaType.Movie));
				await Assert.That(found.Ids).Contains(FightClub);
			});
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches(@"^/library/metadata/matches\?type=1&guid=tmdb"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_WhenTheMatchIsEmpty_FindsNothing()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"MediaContainer":{"size":0}}""");
		var testee = new PlexIdConverter(CreatePlexApi(handler));

		var result = await testee.LookUpAsync(testee.RequestById(FightClub));

		var found = await Assert.That(result).IsFound();
		await Assert.That(found!.Ids).IsEmpty();
	}

	[Test]
	public async Task LookUpAsync_WhenTheMatchIs404_FindsNothing()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);
		var testee = new PlexIdConverter(CreatePlexApi(handler));

		var result = await testee.LookUpAsync(testee.RequestById(FightClub));

		var found = await Assert.That(result).IsFound();
		await Assert.That(found!.Ids).IsEmpty();
	}

	[Test]
	[Arguments(HttpStatusCode.TooManyRequests)]
	[Arguments(HttpStatusCode.ServiceUnavailable)]
	public async Task LookUpAsync_WhenTheMatchIsUnavailable_IsUnavailable(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);
		var testee = new PlexIdConverter(CreatePlexApi(handler));

		var result = await testee.LookUpAsync(testee.RequestById(FightClub));

		await Assert.That(result).IsOfUnionType<Answer<IdLookupResult>, Unavailable>();
	}

	[Test]
	public async Task LookUpAsync_WhenTheLaneRefusesTheMatch_IsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWith(_ => throw new LaneRefusedException("Plex is down."));
		var testee = new PlexIdConverter(CreatePlexApi(handler));

		var result = await testee.LookUpAsync(testee.RequestById(FightClub));

		await Assert.That(result).IsOfUnionType<Answer<IdLookupResult>, Unavailable>();
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
		return CreatePlexApi(handler);
	}

	private static PlexApi CreatePlexApi(TUnit.Mocks.Http.MockHttpHandler handler)
	{
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(handler.CreateClient(PlexBaseUrl), credentialStore, ICurrentUser.Mock().Object);
		return new PlexApi(plexClient, PlexTestUrls.Instance, FeedClock.Today(), NullLogger<PlexApi>.Instance);
	}

	private sealed record PlexByIdPayload(string Json, PlexId PlexId, IReadOnlyList<TmdbId> TmdbIds, ImdbId ImdbId, TvdbId? TvdbId);
}
