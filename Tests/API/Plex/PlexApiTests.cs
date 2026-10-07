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
	public async Task Fills_RateWithEverySourcePlexCarries()
	{
		await Assert
			.That(CreatePlexApi("{}").Fills.Ratings)
			.IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]);
	}

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

		await Assert.That(async () => await testee.SearchAsync("anything", [MediaType.Movie])).Throws<RemoteUnavailableException>();
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

		await Assert.That(async () => await testee.ByIdAsync(PlexId.From("plex-1", MediaType.Movie))).Throws<RemoteUnavailableException>();
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
		await Assert.That(async () => await testee.ByIdAsync(id)).Throws<RemoteUnavailableException>();

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
	public async Task ByIdAsync_WhenPlexRatesTheTitle_MapsEachRatingToASourcePlexSupplies()
	{
		var metadata = ObsessionById() with
		{
			Ratings =
			[
				new("imdb://image.rating", "audience", 7.5),
				new("rottentomatoes://image.rating.ripe", "critic", 10),
				new("rottentomatoes://image.rating.upright", "audience", 8.5),
				new("themoviedb://image.rating", "audience", 7.5),
				new("letterboxd://image.rating", "audience", 8),
			],
			ImdbRatingCount = 9133,
		};
		var testee = CreatePlexApi(ByIdPayloadOf(metadata));

		var found = await Assert.That(await testee.ByIdAsync(PlexId.From(metadata.RatingKey, MediaType.Movie))).IsSuccess();

		var ratings = found!.Details.Ratings.All;
		await Assert
			.That(ratings)
			.IsEquivalentTo(
				new IMediaRating[]
				{
					new ImdbRating(7.5f, Some.Of(9133)),
					new RottenTomatoesCriticsRating(100),
					new RottenTomatoesAudienceRating(85),
					new TmdbRating(7.5f, new None()),
				}
			);
		await Assert.That(ratings.Select(rating => rating.Source)).IsEquivalentTo(RemoteService.Plex.SuppliedRatingSources());
	}

	[Test]
	public async Task ByIdAsync_ForARecordedAnswer_ReadsEveryRatingPlexSends()
	{
		var testee = CreatePlexApi(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Plex", "Recorded", "obsession.json")));

		var found = await Assert.That(await testee.ByIdAsync(PlexId.From("5d77682eeb5d26001f1df4fe", MediaType.Movie))).IsSuccess();

		await Assert
			.That(found!.Details.Ratings)
			.IsEqualTo(
				MediaRatings.From([
					new ImdbRating(7.5f, Some.Of(9133)),
					new RottenTomatoesCriticsRating(100),
					new RottenTomatoesAudienceRating(85),
					new TmdbRating(7.5f, new None()),
				])
			);
	}

	[Test]
	public async Task ByIdAsync_WhenPlexSendsNoRatings_LeavesTheTitleUnrated()
	{
		var testee = CreatePlexApi(ByIdPayloadOf(ObsessionById()));

		var found = await Assert.That(await testee.ByIdAsync(PlexId.From(ObsessionById().RatingKey, MediaType.Movie))).IsSuccess();

		await Assert.That(found!.Details.Ratings).IsEqualTo(MediaRatings.None);
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

	private static string ByIdPayloadOf(PlexTypes.ById.Metadata metadata)
	{
		return JsonSerializer.Serialize(new PlexTypes.ById.Root(new PlexTypes.ById.MediaContainer("1", "Movies", 0, 1, [metadata])));
	}

	private static PlexTypes.ById.Metadata ObsessionById()
	{
		return new PlexTypes.ById.Metadata(
			Art: "/art.jpg",
			RatingKey: "5d77682eeb5d26001f1df4fe",
			Summary: null,
			Tagline: null,
			Type: "movie",
			AddedAt: 0,
			AudienceRating: 8.5,
			OriginallyAvailableAt: "2025-05-16",
			Poster: "/thumb.jpg",
			Title: "Obsession",
			Year: 2025,
			Duration: 0,
			SeasonCount: null,
			EpisodeCount: null,
			Guid: [],
			Genre: []
		);
	}

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
