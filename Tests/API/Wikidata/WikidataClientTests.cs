using System.Net;
using API.Wikidata;
using Domain.Media;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Wikidata;

public class WikidataClientTests
{
	[Test]
	public async Task FindPathAsync_ForAMovie_ReturnsItsPagePath()
	{
		var handler = await AnsweringAsync("project-hail-mary");

		var path = await Assert.That(await Testee(handler).FindPathAsync(ImdbId.From("tt12042730", MediaType.Movie))).IsSuccess();

		await Assert.That(path).IsSome().And.IsEqualTo("m/project_hail_mary");
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*sparql\\?query=.*tt12042730.*"), Times.Once);
	}

	[Test]
	public async Task FindPathAsync_ForAShowThatAlsoListsASeason_ReturnsThePathWithoutTheSeason()
	{
		var path = await Assert.That(await Testee(await AnsweringAsync("bleach")).FindPathAsync(ImdbId.From("tt0434665", MediaType.Show))).IsSuccess();

		await Assert.That(path).IsSome().And.IsEqualTo("tv/bleach");
	}

	[Test]
	public async Task FindPathAsync_WhenWikidataHasNoPage_ReturnsNone()
	{
		var path = await Assert.That(await Testee(await AnsweringAsync("no-page")).FindPathAsync(ImdbId.From("tt0000001", MediaType.Movie))).IsSuccess();

		await Assert.That(path).IsNone();
	}

	[Test]
	public async Task FindPathAsync_WhenWikidataFails_ReturnsTheFailure()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);

		await Assert.That(await Testee(handler).FindPathAsync(ImdbId.From("tt12042730", MediaType.Movie))).IsError();
	}

	[Test]
	public async Task FindPathAsync_ForAnIdThatIsNoImdbTitle_FailsWithoutAsking()
	{
		var handler = await AnsweringAsync("project-hail-mary");

		await Assert.That(await Testee(handler).FindPathAsync(ImdbId.From("tt1\" } SELECT", MediaType.Movie))).IsError();
		await Assert.That(handler.Requests).IsEmpty();
	}

	[Test]
	public async Task FindPathAsync_WhenWikidataCannotBeReached_ReturnsTheFailureInsteadOfThrowing()
	{
		var client = new HttpClient(new Unreachable()) { BaseAddress = new Uri("https://query.wikidata.org/") };

		await Assert.That(await new WikidataClient(client).FindPathAsync(ImdbId.From("tt12042730", MediaType.Movie))).IsError();
	}

	[Test]
	public async Task FindPathAsync_ForAPathThatIsNoTitlePage_IgnoresIt()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"results":{"bindings":[{"path":{"type":"literal","value":"franchise/marvel"}},{"path":{"type":"literal","value":"m/the_avengers_2012"}}]}}"""
			);

		var path = await Assert.That(await Testee(handler).FindPathAsync(ImdbId.From("tt0848228", MediaType.Movie))).IsSuccess();

		await Assert.That(path).IsSome().And.IsEqualTo("m/the_avengers_2012");
	}

	private sealed class Unreachable : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			throw new HttpRequestException("No route to host.");
		}
	}

	private static async Task<MockHttpHandler> AnsweringAsync(string recording)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Wikidata", "Recorded", $"{recording}.json")));
		return handler;
	}

	private static WikidataClient Testee(MockHttpHandler handler) => new(handler.CreateClient("https://query.wikidata.org/"));
}
