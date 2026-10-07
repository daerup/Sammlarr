using System.Net;
using API.Wikidata;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
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
			.IsSuccess()
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
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*sparql\\?query=.*tt12042730.*"), Times.Once);
	}

	[Test]
	[Arguments(MediaType.Movie, "P4947", "P12196")]
	[Arguments(MediaType.Show, "P4983", "P4835")]
	public async Task LookUpAsync_ForEachType_AsksForThatTypesTmdbAndTvdbIds(MediaType type, string tmdb, string tvdb)
	{
		var handler = await AnsweringAsync("no-page");
		var testee = Testee(handler);

		await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", type)));

		handler.Verify(request => request.PathMatches($".*wdt%3A{tmdb}%20%3Ftmdb.*wdt%3A{tvdb}%20%3Ftvdb.*"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForAShowThatAlsoListsASeason_FindsTheShowsPageAndItsShowIds()
	{
		var testee = Testee(await AnsweringAsync("bleach"));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0434665", MediaType.Show)));

		await Assert
			.That(result)
			.IsSuccess()
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
				"""{"results":{"bindings":[{"tmdb":{"type":"literal","value":"tt0848228"},"metacritic":{"type":"literal","value":"game/the-avengers"},"letterboxd":{"type":"literal","value":"film/the-avengers-2012"}}]}}"""
			);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0848228", MediaType.Movie)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task LookUpAsync_WhenWikidataHasNoPage_FindsNoIdAndNoHints()
	{
		var testee = Testee(await AnsweringAsync("no-page"));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Movie)));

		await Assert
			.That(result)
			.IsSuccess()
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
				"""{"results":{"bindings":[{"rt":{"type":"literal","value":"franchise/marvel"}},{"rt":{"type":"literal","value":"m/the_avengers_2012"}}]}}"""
			);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0848228", MediaType.Movie)));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
				await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { RottenTomatoesId.From("m/the_avengers_2012", MediaType.Movie) })
			);
	}

	[Test]
	public async Task LookUpAsync_ForAnItemThatOnlyListsASeasonPage_FindsNoId()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"results":{"bindings":[{"rt":{"type":"literal","value":"tv/small_axe/s02"}}]}}""");
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt10551102", MediaType.Show)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
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

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<RemoteUnavailableException>()).IsSome());
	}

	[Test]
	public async Task LookUpAsync_WhenWikidataRejectsTheQuery_FailsAsAnAnswer()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.BadRequest);
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", MediaType.Movie)));

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<RemoteUnavailableException>()).IsNone());
	}

	[Test]
	public async Task LookUpAsync_ForAnIdThatIsNoImdbTitle_FailsWithoutAsking()
	{
		var handler = await AnsweringAsync("project-hail-mary");
		var testee = Testee(handler);

		await Assert.That(await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt1\" } SELECT", MediaType.Movie)))).IsError();
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
						[MediaProvider.RottenTomatoes, MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Metacritic, MediaProvider.Letterboxd],
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

	private static WikidataIdConverter Testee(MockHttpHandler handler) => new(handler.CreateClient("https://query.wikidata.org/"));
}
