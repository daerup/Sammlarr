using System.Net;
using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexApiTests
{
	private const string PlexBaseUrl = "https://plex.tv";

	[Test]
	public async Task SearchAsync_ReturnsRawDetails_WithDefaultMediaId()
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
			await Assert.That(r.Identity.Id).IsEqualTo(default);
			await Assert.That(r.Identity.Externals).Contains(PlexId.From(plexValue, MediaType.Movie));
		}
	}

	[Test]
	public async Task SearchAsync_WhenResponseIsNotSuccess_Throws()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);
		var httpClient = handler.CreateClient(PlexBaseUrl);
		var credentialStore = ICredentialRepository.Mock();
		credentialStore
			.GetAsync(WatchlistProvider.Plex, Any())
			.Returns(new TestCredential(WatchlistProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue));
		var testee = new PlexApi(new PlexClient(httpClient, credentialStore), NullLogger<PlexApi>.Instance);

		await Assert.That(async () => await testee.SearchAsync("anything", [MediaType.Movie])).Throws<HttpRequestException>();
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

	private static PlexApi CreatePlexApi(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var httpClient = handler.CreateClient(PlexBaseUrl);
		var credential = new TestCredential(WatchlistProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialRepository.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).Returns(credential);
		var plexClient = new PlexClient(httpClient, credentialStore);
		return new PlexApi(plexClient, NullLogger<PlexApi>.Instance);
	}
}
