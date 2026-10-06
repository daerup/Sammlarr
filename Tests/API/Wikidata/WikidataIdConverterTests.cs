using System.Net;
using API.Wikidata;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Wikidata;

public class WikidataIdConverterTests
{
	[Test]
	public async Task LookUpAsync_ForAMovie_FindsItsRottenTomatoesId()
	{
		var handler = await AnsweringAsync("project-hail-mary");
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt12042730", MediaType.Movie)));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
				await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { RottenTomatoesId.From("m/project_hail_mary", MediaType.Movie) })
			);
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*sparql\\?query=.*tt12042730.*"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForAShowThatAlsoListsASeason_FindsTheShowsPage()
	{
		var testee = Testee(await AnsweringAsync("bleach"));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0434665", MediaType.Show)));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found => await Assert.That(found.Ids).IsEquivalentTo(new IExternalMediaId[] { RottenTomatoesId.From("tv/bleach", MediaType.Show) }));
	}

	[Test]
	public async Task LookUpAsync_WhenWikidataHasNoPage_FindsNoIdAndNoHints()
	{
		var testee = Testee(await AnsweringAsync("no-page"));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0000001", MediaType.Movie)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found).IsEqualTo(new IdLookupResult([], new None())));
	}

	[Test]
	public async Task LookUpAsync_ForAPathThatIsNoTitlePage_IgnoresIt()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"results":{"bindings":[{"path":{"type":"literal","value":"franchise/marvel"}},{"path":{"type":"literal","value":"m/the_avengers_2012"}}]}}"""
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
		handler.OnAnyRequest().RespondWithJson("""{"results":{"bindings":[{"path":{"type":"literal","value":"tv/small_axe/s02"}}]}}""");
		var testee = Testee(handler);

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt10551102", MediaType.Show)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task LookUpAsync_WhenWikidataIsUnavailable_SaysTheProviderIsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);
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
	public async Task Lookups_TurnAnImdbIdIntoARottenTomatoesIdWithoutHints()
	{
		var testee = Testee(Mock.HttpHandler());

		await Assert.That(testee.Lookups).IsEquivalentTo([(IdLookup)new ById([MediaProvider.Imdb], [MediaProvider.RottenTomatoes], 1, GivesHints: false)]);
	}

	private static async Task<MockHttpHandler> AnsweringAsync(string recording)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Wikidata", "Recorded", $"{recording}.json")));
		return handler;
	}

	private static WikidataIdConverter Testee(MockHttpHandler handler) => new(handler.CreateClient("https://query.wikidata.org/"));
}
