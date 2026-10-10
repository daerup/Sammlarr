using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.Tests.Feeds;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task LookUpAsync_ForAPlexId_ReturnsEveryGuidPlexHolds()
	{
		var payload = CreatePlexByIdPayload(true, false, true);
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json), PlexStub.NoSearchCache());

		var result = await testee.LookUpAsync(testee.RequestById(payload.PlexId));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).Contains(payload.PlexId);
				await Assert.That(found.Ids).Contains(payload.TmdbIds[0]);
				await Assert.That(found.Ids).Contains(payload.ImdbId);
				await Assert.That(found.Ids).Contains(payload.TvdbId!.Value);
			});
	}

	[Test]
	public async Task LookUpAsync_ForAPlexIdWithTwoTmdbGuids_ReturnsBothAndTheOtherGuids()
	{
		var payload = CreatePlexByIdPayload(true, true, true);
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json), PlexStub.NoSearchCache());

		var result = await testee.LookUpAsync(testee.RequestById(payload.PlexId));

		await Assert
			.That(result)
			.IsSuccess()
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
		var testee = new PlexIdConverter(CreatePlexApi(payload.Json), PlexStub.NoSearchCache());

		var result = await testee.LookUpAsync(testee.RequestById(payload.PlexId));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).Contains(payload.PlexId);
				await Assert.That(found.Ids).Contains(payload.ImdbId);
				await Assert.That(found.Ids.Any(id => id.Source == MediaProvider.Tmdb)).IsFalse();
			});
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
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(handler.CreateClient(PlexBaseUrl), credentialStore, ICurrentUser.Mock().Object);
		return new PlexApi(plexClient, PlexTestUrls.Instance, FeedClock.Today(), NullLogger<PlexApi>.Instance);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, IReadOnlyList<TmdbId> TmdbIds, ImdbId ImdbId, TvdbId? TvdbId);
}
