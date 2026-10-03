using System.Net;
using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexApiTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task SearchAsync_ForAPlexHit_ReturnsItWithItsPlexId()
	{
		var faker = new Faker();
		var plexValue = $"plex-{faker.Random.AlphaNumeric(10)}";
		var json = BuildSearchPayload(plexValue, faker.Lorem.Sentence(2));
		var testee = CreatePlexApi(json);

		var results = await testee.SearchAsync("anything", [MediaType.Movie]);

		using var _ = Assert.Multiple();
		await Assert.That(results).IsNotEmpty();
		foreach (var r in results)
		{
			await Assert.That(r.Externals).Contains(PlexId.From(plexValue, MediaType.Movie));
		}
	}

	[Test]
	[Arguments(HttpStatusCode.TooManyRequests)]
	[Arguments(HttpStatusCode.RequestTimeout)]
	[Arguments(HttpStatusCode.ServiceUnavailable)]
	[Arguments(HttpStatusCode.BadGateway)]
	public async Task SearchAsync_WhenPlexIsUnavailable_ThrowsProviderUnavailable(HttpStatusCode status)
	{
		var testee = CreateFailingPlexApi(status);

		await Assert.That(async () => await testee.SearchAsync("anything", [MediaType.Movie])).Throws<ProviderUnavailableException>();
	}

	[Test]
	public async Task SearchAsync_WhenResponseFailsPermanently_ThrowsHttpRequest()
	{
		var testee = CreateFailingPlexApi(HttpStatusCode.NotFound);

		await Assert.That(async () => await testee.SearchAsync("anything", [MediaType.Movie])).Throws<HttpRequestException>();
	}

	[Test]
	public async Task ByIdAsync_WhenPlexIsUnavailable_ThrowsProviderUnavailable()
	{
		var testee = CreateFailingPlexApi(HttpStatusCode.TooManyRequests);

		await Assert.That(async () => await testee.ByIdAsync(PlexId.From("plex-1", MediaType.Movie))).Throws<ProviderUnavailableException>();
	}

	[Test]
	public async Task ByIdAsync_WhenPlexHasNoMetadata_ReturnsAPermanentError()
	{
		var testee = CreatePlexApi(BuildEmptyByIdPayload());

		var result = await testee.ByIdAsync(PlexId.From("plex-1", MediaType.Movie));

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsNone());
	}

	[Test]
	public async Task ByIdAsync_AfterPlexWasUnavailable_AsksPlexAgain()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWith(new HttpResponseMessage(HttpStatusCode.TooManyRequests)).Then().RespondWithJson(BuildEmptyByIdPayload());
		var testee = BuildPlexApi(handler.CreateClient(PlexBaseUrl));
		var id = PlexId.From("plex-1", MediaType.Movie);
		await Assert.That(async () => await testee.ByIdAsync(id)).Throws<ProviderUnavailableException>();

		var retried = await testee.ByIdAsync(id);

		await Assert.That(retried).IsError();
		await Assert.That(handler.Requests).Count().IsEqualTo(2);
	}

	[Test]
	public async Task ByIdAsync_ForATitleAlreadyFetched_AnswersFromTheFirstFetch()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(BuildEmptyByIdPayload());
		var testee = BuildPlexApi(handler.CreateClient(PlexBaseUrl));
		var id = PlexId.From("plex-1", MediaType.Movie);
		await testee.ByIdAsync(id);

		await testee.ByIdAsync(id);

		await Assert.That(handler.Requests).Count().IsEqualTo(1);
	}

	[Test]
	public async Task SearchAsync_WhenPlexSendsBlankValuesAndZeroCounts_LeavesThoseDetailsUnknown()
	{
		var metadata = new PlexTypes.Search.Metadata(
			RatingKey: "plex-9",
			Title: " ",
			Thumb: "",
			Art: null,
			Type: "show",
			Year: 0,
			Duration: 0,
			Genre: [new PlexTypes.Search.Genre(" ")],
			OriginallyAvailableAt: "",
			SeasonCount: 0,
			EpisodeCount: 0
		);
		var testee = CreatePlexApi(SearchPayloadOf(metadata));

		var results = await testee.SearchAsync("anything", [MediaType.Show]);

		await Assert.That(results.Single().Details).IsEqualTo(new ShowDetails());
	}

	[Test]
	public async Task SearchAsync_WhenNoMatches_ReturnsEmpty()
	{
		var json = BuildEmptySearchPayload();
		var testee = CreatePlexApi(json);

		var results = await testee.SearchAsync("anything", [MediaType.Movie]);

		await Assert.That(results).IsEmpty();
	}

	private static string BuildSearchPayload(string plexValue, string title)
	{
		var metadata = new PlexTypes.Search.Metadata(
			RatingKey: plexValue,
			Title: title,
			Thumb: null,
			Art: null,
			Type: "movie",
			Year: 2024,
			Duration: 6_000_000,
			Genre: null,
			OriginallyAvailableAt: "2024-01-01",
			SeasonCount: null,
			EpisodeCount: null
		);
		return SearchPayloadOf(metadata);
	}

	private static string SearchPayloadOf(PlexTypes.Search.Metadata metadata)
	{
		var nested = new PlexTypes.Search.NestedSearchResult(metadata);
		var searchResult = new PlexTypes.Search.SearchResult(Size: 1, NestedSearchResult: [nested]);
		var container = new PlexTypes.Search.MediaContainer(SuggestedTerms: [], Identifier: "search", Size: 1, SearchResults: [searchResult]);
		return JsonSerializer.Serialize(new PlexTypes.Search.Root(container));
	}

	private static string BuildEmptySearchPayload()
	{
		var emptyResult = new PlexTypes.Search.SearchResult(Size: 0, NestedSearchResult: []);
		var container = new PlexTypes.Search.MediaContainer(SuggestedTerms: [], Identifier: "search", Size: 1, SearchResults: [emptyResult]);
		return JsonSerializer.Serialize(new PlexTypes.Search.Root(container));
	}

	private static string BuildEmptyByIdPayload() =>
		JsonSerializer.Serialize(new PlexTypes.ById.Root(new PlexTypes.ById.MediaContainer("1", "Movies", 0, 0, [])));

	private static PlexApi CreateFailingPlexApi(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);
		return BuildPlexApi(handler.CreateClient(PlexBaseUrl));
	}

	private static PlexApi CreatePlexApi(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		return BuildPlexApi(handler.CreateClient(PlexBaseUrl));
	}

	private static PlexApi BuildPlexApi(HttpClient httpClient)
	{
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		return new PlexApi(new PlexClient(httpClient, credentialStore, ICurrentUser.Mock().Object), PlexTestUrls.Instance, NullLogger<PlexApi>.Instance);
	}
}
