using System.Collections.Concurrent;
using System.Net;
using System.Text;
using API.Http.Lanes;
using API.Wikidata;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Wikidata;

public class WikidataIdConverterTests
{
	[Test]
	public async Task LookUpAsync_ForAMovie_FindsEveryIdWikidataHoldsAsAMovie()
	{
		var handler = await AnsweringAsync("project-hail-mary");
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", MediaType.Movie)));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
				await Assert
					.That(found.Ids)
					.IsEquivalentTo(
						new IExternalMediaId[]
						{
							RottenTomatoesId.From("m/project_hail_mary", MediaType.Movie),
							TmdbId.From("687163", MediaType.Movie),
							TvdbId.From("346729", MediaType.Movie),
							MetacriticId.From("project-hail-mary", MediaType.Movie),
							LetterboxdId.From("project-hail-mary", MediaType.Movie),
						}
					)
			);
		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(".*sparql$"), Times.Once);
	}

	[Test]
	[Arguments(MediaType.Movie, "P4947", "P12196")]
	[Arguments(MediaType.Show, "P4983", "P4835")]
	public async Task LookUpAsync_ForEachType_AsksForThatTypesTmdbAndTvdbIds(MediaType type, string tmdb, string tvdb)
	{
		var bodies = new ConcurrentQueue<string>();
		var testee = Testee(Capturing(bodies, """{"results":{"bindings":[]}}"""));

		await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", type)));

		await Assert.That(bodies.Single()).Contains($"wdt:{tmdb} ?tmdb").And.Contains($"wdt:{tvdb} ?tvdb");
	}

	[Test]
	public async Task LookUpAsync_ForIdsWaitingBehindAQuery_SendsThemInOneQuery()
	{
		using var release = new ManualResetEventSlim();
		var bodies = new ConcurrentQueue<string>();
		var testee = Testee(Capturing(bodies, """{"results":{"bindings":[]}}""", release));
		var first = testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Movie)));
		Task[] waiting =
		[
			.. new[] { "tt0000002", "tt0000003", "tt0000004" }.Select(imdb => testee.LookUpAsync(testee.RequestById(ImdbId.From(imdb, MediaType.Movie)))),
		];

		release.Set();
		await Task.WhenAll([first, .. waiting]);

		await Assert.That(bodies).Count().IsEqualTo(2);
		await Assert.That(bodies.Last()).Contains("\"tt0000002\" \"tt0000003\" \"tt0000004\"");
	}

	[Test]
	public async Task LookUpAsync_WhenAQueryFails_FailsEveryIdItCarried()
	{
		using var release = new ManualResetEventSlim();
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				release.Wait(TimeSpan.FromSeconds(10));
				return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
			});
		var testee = Testee(handler);
		var first = testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Movie)));
		Task<Answer<IdLookupResult>>[] waiting =
		[
			.. new[] { "tt0000002", "tt0000003", "tt0000004" }.Select(imdb => testee.LookUpAsync(testee.RequestById(ImdbId.From(imdb, MediaType.Movie)))),
		];

		release.Set();
		var answers = await Task.WhenAll(waiting);

		await first;
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Exactly(2));
		foreach (var answer in answers)
		{
			await Assert.That(answer).IsOfUnionType<Answer<IdLookupResult>, Unavailable>();
		}
	}

	[Test]
	public async Task LookUpAsync_ForOneQueryAnsweringSeveralIds_GivesEachIdItsOwnRows()
	{
		using var release = new ManualResetEventSlim();
		var bodies = new ConcurrentQueue<string>();
		var testee = Testee(
			Capturing(bodies, """{"results":{"bindings":[{"imdb":{"type":"literal","value":"tt0000002"},"rt":{"type":"literal","value":"m/second"}}]}}""", release)
		);
		var first = testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Movie)));
		var second = testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000002", MediaType.Movie)));
		var third = testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000003", MediaType.Movie)));

		release.Set();
		await Task.WhenAll(first, second, third);

		await Assert
			.That(await second)
			.IsFound()
			.And.Multiple(async found => await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { RottenTomatoesId.From("m/second", MediaType.Movie) }));
		await Assert.That(await third).IsFound().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
		await Assert.That(bodies.Last()).Contains("\"tt0000002\" \"tt0000003\"");
	}

	[Test]
	public async Task LookUpAsync_ForAShowThatAlsoListsASeason_FindsTheShowsPageAndItsShowIds()
	{
		var testee = Testee(await AnsweringAsync("bleach"));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0434665", MediaType.Show)));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
				await Assert
					.That(found.Ids)
					.IsEquivalentTo(
						new IExternalMediaId[]
						{
							RottenTomatoesId.From("tv/bleach", MediaType.Show),
							TmdbId.From("30984", MediaType.Show),
							TvdbId.From("74796", MediaType.Show),
						}
					)
			);
	}

	[Test]
	public async Task LookUpAsync_ForValuesThatAreNoIds_IgnoresThem()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"results":{"bindings":[{"imdb":{"type":"literal","value":"tt0848228"},"tmdb":{"type":"literal","value":"tt0848228"},"metacritic":{"type":"literal","value":"game/the-avengers"},"letterboxd":{"type":"literal","value":"film/the-avengers-2012"}}]}}"""
			);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0848228", MediaType.Movie)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task LookUpAsync_WhenWikidataHasNoPage_FindsNoIdAndNoHints()
	{
		var testee = Testee(await AnsweringAsync("no-page"));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Movie)));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).IsEmpty();
				await Assert.That(found.Hints).IsNone();
			});
	}

	[Test]
	public async Task LookUpAsync_ForAPathThatIsNoTitlePage_IgnoresIt()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"results":{"bindings":[{"imdb":{"type":"literal","value":"tt0848228"},"rt":{"type":"literal","value":"franchise/marvel"}},{"imdb":{"type":"literal","value":"tt0848228"},"rt":{"type":"literal","value":"m/the_avengers_2012"}}]}}"""
			);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0848228", MediaType.Movie)));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
				await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { RottenTomatoesId.From("m/the_avengers_2012", MediaType.Movie) })
			);
	}

	[Test]
	public async Task LookUpAsync_ForAnItemThatOnlyListsASeasonPage_FindsNoId()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson("""{"results":{"bindings":[{"imdb":{"type":"literal","value":"tt10551102"},"rt":{"type":"literal","value":"tv/small_axe/s02"}}]}}""");
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt10551102", MediaType.Show)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	[Arguments(HttpStatusCode.ServiceUnavailable)]
	[Arguments(HttpStatusCode.TooManyRequests)]
	public async Task LookUpAsync_WhenWikidataIsUnavailable_SaysTheProviderIsUnavailable(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", MediaType.Movie)));

		await Assert.That(result).IsOfUnionType<Answer<IdLookupResult>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Wikidata, new None(), true));
	}

	[Test]
	public async Task LookUpAsync_WhenWikidataRejectsTheQuery_IsUnavailableAndSent()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.BadRequest);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", MediaType.Movie)));

		await Assert.That(result).IsOfUnionType<Answer<IdLookupResult>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Wikidata, new None(), true));
	}

	[Test]
	public async Task LookUpAsync_ForAnIdThatIsNoImdbTitle_FailsWithoutAsking()
	{
		var handler = await AnsweringAsync("project-hail-mary");
		var testee = Testee(handler);

		await Assert
			.That(await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt1\" } SELECT", MediaType.Movie))))
			.IsOfUnionType<Answer<IdLookupResult>, NoMatch>();
		await Assert.That(handler.Requests).IsEmpty();
	}

	[Test]
	public async Task Lookups_TurnAnImdbIdIntoItsPageAndCrossIdsAsTheLastResortWithoutHints()
	{
		var testee = Testee(Mock.HttpHandler());

		await Assert
			.That(testee.Lookups)
			.IsEquivalentTo([
				(IdLookup)
					new ById(
						[MediaProvider.Imdb],
						[
							MediaProvider.RottenTomatoes,
							MediaProvider.Tmdb,
							MediaProvider.Tvdb,
							MediaProvider.Metacritic,
							MediaProvider.Letterboxd,
							MediaProvider.AniList,
							MediaProvider.Mal,
						],
						3,
						GivesHints: false
					),
			]);
	}

	private static async Task<MockHttpHandler> AnsweringAsync(string recording)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Wikidata", "Recorded", $"{recording}.json")));
		return handler;
	}

	private static WikidataIdConverter Testee(MockHttpHandler handler) => new(handler.CreateClient("https://query.wikidata.org/"), Lanes());

	private static MockHttpHandler Capturing(ConcurrentQueue<string> bodies, string json, ManualResetEventSlim? release = null)
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(request =>
			{
				release?.Wait(TimeSpan.FromSeconds(10));
				bodies.Enqueue(WebUtility.UrlDecode(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult()));
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/sparql-results+json") };
			});
		return handler;
	}

	private static Lanes Lanes()
	{
		var options = new WikidataOptions
		{
			BaseUrl = "https://query.wikidata.org",
			UserAgent = "Sammlarr tests",
			Http = new ProviderHttpOptions { Lanes = new() { ["sparql"] = new LaneOptions { MaxBatch = 100 } } },
		};
		return new(TimeProvider.System, [new RemoteSettings(RemoteService.Wikidata, options)], new ServiceCollection().BuildServiceProvider());
	}
}
