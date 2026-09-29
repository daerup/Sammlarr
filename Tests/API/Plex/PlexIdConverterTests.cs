using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.Tmdb;
using API.Tmdb.Types;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexIdConverterTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsPlexId_ReturnsPlexIdWithAdditionalIds()
	{
		var payload = CreatePlexByIdPayload(true, true);
		var testee = new PlexIdConverter(null!, CreatePlexApi(payload.Json));

		var result = await testee.ConvertAsync([payload.PlexId], new None());

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async externals =>
			{
				await Assert.That(externals).Contains(payload.PlexId);
				await Assert.That(externals).Contains(payload.TmdbId);
				await Assert.That(externals).Contains(payload.ImdbId!.Value);
				await Assert.That(externals).Contains(payload.TvdbId!.Value);
			});
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbAndSearchMatches_ReturnsResolvedPlexId()
	{
		var payload = CreatePlexByIdPayload(true, true);
		var tmdbApi = CreateTmdbApi(payload.TmdbId, "match");
		var testee = new PlexIdConverter(tmdbApi, CreatePlexApiWithSearch(payload));

		var result = await testee.ConvertAsync([payload.TmdbId], new None());

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async externals =>
			{
				await Assert.That(externals).Contains(payload.PlexId);
				await Assert.That(externals).Contains(payload.TmdbId);
				await Assert.That(externals).Contains(payload.ImdbId!.Value);
				await Assert.That(externals).Contains(payload.TvdbId!.Value);
			});
	}

	[Test]
	public async Task ConvertAsync_WhenMediaIdIsTmdbAndSearchHasNoMatch_ReturnsFailed()
	{
		var payload = CreatePlexByIdPayload(false, false);
		var tmdbApi = CreateTmdbApi(payload.TmdbId, "match");
		var testee = new PlexIdConverter(tmdbApi, CreatePlexApiWithEmptySearch());

		var result = await testee.ConvertAsync([payload.TmdbId], new None());
		await Assert.That(result).IsError();
	}

	private static TmdbApi CreateTmdbApi(TmdbId tmdbId, string originalTitle)
	{
		var details = new TmdbTypes.Details(int.Parse(tmdbId.Value), originalTitle, null, "2021-01-01", null, originalTitle, null, "movie");
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(JsonSerializer.Serialize(details));
		var apiKeyStore = IApiKeyService.Mock();
		IApiKey apiKey = new TestApiKey(ApiKeyProvider.Tmdb, "test-api-key");
		apiKeyStore.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of(apiKey));
		var options = new TmdbOptions { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p" };
		var tmdbClient = new TmdbClient(handler.CreateClient(options.BaseUrl), options, apiKeyStore.Object, ICurrentUser.Mock().Object);
		return new TmdbApi(tmdbClient, apiKeyStore.Object, NullLogger<TmdbApi>.Instance);
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
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);
		return new PlexApi(plexClient, PlexTestUrls.Instance, NullLogger<PlexApi>.Instance);
	}

	private static PlexApi CreatePlexApiWithEmptySearch()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(BuildEmptySearchJson());
		var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);
		return new PlexApi(plexClient, PlexTestUrls.Instance, NullLogger<PlexApi>.Instance);
	}

	private static PlexApi CreatePlexApiWithSearch(PlexByIdPayload payload)
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(r => r.PathStartsWith("/library/search")).RespondWithJson(BuildSearchJson(payload.PlexId));
		handler.OnRequest(r => r.PathStartsWith("/library/metadata")).RespondWithJson(payload.Json);
		var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object);
		return new PlexApi(plexClient, PlexTestUrls.Instance, NullLogger<PlexApi>.Instance);
	}

	private static string BuildEmptySearchJson()
	{
		var root = new PlexTypes.Search.Root(new PlexTypes.Search.MediaContainer([], "com.plexapp.plugins.library", 1, [new PlexTypes.Search.SearchResult(0, [])]));
		return JsonSerializer.Serialize(root);
	}

	private static string BuildSearchJson(PlexId plexId)
	{
		var metadata = new PlexTypes.Search.Metadata(plexId.Value, "match", null, null, "movie", 2021, null, null, null, null, null);
		var root = new PlexTypes.Search.Root(
			new PlexTypes.Search.MediaContainer(
				[],
				"com.plexapp.plugins.library",
				1,
				[new PlexTypes.Search.SearchResult(1, [new PlexTypes.Search.NestedSearchResult(metadata)])]
			)
		);
		return JsonSerializer.Serialize(root);
	}

	private record PlexByIdPayload(string Json, PlexId PlexId, TmdbId TmdbId, ImdbId? ImdbId, TvdbId? TvdbId);
}
