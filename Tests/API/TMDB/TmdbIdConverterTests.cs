using System.Net;
using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.Tests.Plex;
using API.Tmdb;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbId_ReturnsSameId()
	{
		var testee = new TmdbIdConverter(null!, null!);
		var tmdbId = TmdbId.From("100", MediaType.Movie);

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Tmdb), [tmdbId], new None());

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(tmdbId));
	}

	[Test]
	public async Task ConvertAsync_WhenPlexMetadataContainsTmdbIds_ReturnsFirstTmdbAndOtherIds()
	{
		var payload = CreatePlexByIdPayload(true, true, true);
		var testee = new TmdbIdConverter(CreatePlexApi(payload.Json), null!);

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Plex), [payload.PlexId], new None());

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async externals =>
			{
				await Assert.That(externals).Contains(payload.TmdbIds[0]);
				await Assert.That(externals).Contains(payload.TmdbIds[1]);
				await Assert.That(externals).Contains(payload.PlexId);
				await Assert.That(externals).Contains(payload.ImdbId);
				await Assert.That(externals).Contains(payload.TvdbId!.Value);
			});
	}

	[Test]
	public async Task ConvertAsync_WhenPlexMetadataHasNoTmdbIds_ReturnsUnmatchedWithOtherIds()
	{
		var payload = CreatePlexByIdPayload(false, false, false);
		var testee = new TmdbIdConverter(CreatePlexApi(payload.Json), null!);

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Plex), [payload.PlexId], new None());

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task ConvertAsync_FromAnImdbId_FindsTheTmdbIdThroughTmdb()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(".*find/tt0137523.*external_source=imdb_id.*"))
			.RespondWithJson("{\"movie_results\":[{\"id\":550}],\"tv_results\":[]}");
		var testee = new TmdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Imdb), [ImdbId.From("tt0137523", MediaType.Movie)], new None());

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(TmdbId.From("550", MediaType.Movie)));
	}

	[Test]
	public async Task ConvertAsync_WhenTmdbKnowsOnlyTheSecondImdbId_FindsTheTmdbIdThroughIt()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(".*find/tt0000001.*")).RespondWithJson("{\"movie_results\":[],\"tv_results\":[]}");
		handler.OnRequest(request => request.PathMatches(".*find/tt0137523.*")).RespondWithJson("{\"movie_results\":[{\"id\":550}],\"tv_results\":[]}");
		var testee = new TmdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(
			testee.From(MediaProvider.Imdb),
			[ImdbId.From("tt0000001", MediaType.Movie), ImdbId.From("tt0137523", MediaType.Movie)],
			new None()
		);

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(TmdbId.From("550", MediaType.Movie)));
	}

	[Test]
	public async Task ConvertAsync_FromATvdbIdWhileTmdbIsRateLimited_FailsAsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);
		var testee = new TmdbIdConverter(null!, TmdbApis.Over(handler));

		var result = await testee.ConvertAsync(testee.From(MediaProvider.Tvdb), [TvdbId.From("81189", MediaType.Show)], new None());

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsTypeOf<ProviderUnavailableException>());
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
		credentialStore.GetAsync(AuthProvider.Plex, CancellationToken.None).Returns(credential);
		var plexClient = new PlexClient(handler.CreateClient(PlexBaseUrl), credentialStore, ICurrentUser.Mock().Object);
		return new PlexApi(plexClient, PlexTestUrls.Instance, NullLogger<PlexApi>.Instance);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, IReadOnlyList<TmdbId> TmdbIds, ImdbId ImdbId, TvdbId? TvdbId);
}
