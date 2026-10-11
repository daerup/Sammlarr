using System.Net;
using API.Tests.Feeds;
using API.Tmdb;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Domain.Auth;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using TUnit.Mocks.Http;

namespace API.Tests.TMDB;

public class TmdbFeedServiceTests
{
	private const string PageOne = """
		{"page":1,"total_pages":2,"items":[
		{"id":550,"media_type":"movie","title":"Fight Club","release_date":"1999-10-15"},
		{"id":1396,"media_type":"tv","name":"Breaking Bad","first_air_date":"2008-01-20"}]}
		""";

	private const string PageTwo = """{"page":2,"total_pages":2,"items":[{"id":27205,"media_type":"movie","title":"Inception","release_date":"2010-07-15"}]}""";

	private readonly List<IdentifyRequest> _asked = [];

	[Test]
	[Arguments("8231234", "8231234")]
	[Arguments("https://www.themoviedb.org/list/8231234", "8231234")]
	[Arguments("https://www.themoviedb.org/list/8231234-best-of-2026?language=en", "8231234")]
	[Arguments("themoviedb.org/list/8231234/", "8231234")]
	public async Task NormalizeAsync_ForANumberOrALink_StoresTheListsNumber(string input, string number)
	{
		var result = await Testee(Mock.HttpHandler()).NormalizeAsync(input);

		await Assert.That(result).IsSuccess().And.IsEqualTo(number);
	}

	[Test]
	[Arguments("snoak/trakt-s-trending-shows")]
	[Arguments("https://www.themoviedb.org/movie/550")]
	public async Task NormalizeAsync_ForAnythingElse_IsAnError(string input)
	{
		await Assert.That(await Testee(Mock.HttpHandler()).NormalizeAsync(input)).IsError();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAMixedListAcrossPages_AsksForEveryTitleWithItsTypeAndHints()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=1&api_key=tmdb-key$")).RespondWithJson(PageOne);
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=2&api_key=tmdb-key$")).RespondWithJson(PageTwo);

		await Testee(handler).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		await Assert
			.That(_asked.Select(request => $"{request.Candidates.Single()} {request.Hints.Map(hints => $"{hints.Title} {hints.Year}").GetValueOr("none")}"))
			.IsEquivalentTo(["Tmdb:Movie:550 Fight Club 1999", "Tmdb:Show:1396 Breaking Bad 2008", "Tmdb:Movie:27205 Inception 2010"]);
	}

	[Test]
	[Arguments("https://www.themoviedb.org/movie/top-rated", "movie/top-rated")]
	[Arguments("https://www.themoviedb.org/movie/now-playing?view=calendar", "movie/now-playing")]
	[Arguments("https://www.themoviedb.org/discover/tv?with_genres=16", "discover/tv?with_genres=16")]
	[Arguments("https://www.themoviedb.org/keyword/9715-superhero/movie", "keyword/9715/movie")]
	public async Task NormalizeAsync_ForASitePage_StoresItsPathAndQuery(string input, string stored)
	{
		await Assert.That(await Testee(Mock.HttpHandler()).NormalizeAsync(input)).IsSuccess().And.IsEqualTo(stored);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForACategoryPage_ReadsEveryPageAndAsksEachResultByThePagesType()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/3/tv/top_rated\?page=1&api_key=tmdb-key$"))
			.RespondWithJson("""{"page":1,"total_pages":2,"results":[{"id":1396,"name":"Breaking Bad","first_air_date":"2008-01-20"}]}""");
		handler
			.OnRequest(request => request.PathMatches(@"^/3/tv/top_rated\?page=2&api_key=tmdb-key$"))
			.RespondWithJson("""{"page":2,"total_pages":2,"results":[{"id":1399,"name":"Game of Thrones","first_air_date":"2011-04-17"}]}""");

		await Testee(handler).GetFeedItemsAsync("tv/top-rated").InListOrderAsync().ToListAsync();

		await Assert.That(_asked.Select(request => $"{request.Candidates.Single()}")).IsEquivalentTo(["Tmdb:Show:1396", "Tmdb:Show:1399"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPage_StopsAtFiveHundredTitles()
	{
		var handler = Mock.HttpHandler();
		var asked = 0;
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				var page = Interlocked.Increment(ref asked);
				var results = Enumerable.Range(((page - 1) * 20) + 1, 20).Select(id => $$$"""{"id":{{{id}}},"title":"Movie {{{id}}}"}""");
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("{\"total_pages\":500,\"results\":[" + string.Join(",", results) + "]}", System.Text.Encoding.UTF8, "application/json"),
				};
			});

		await Testee(handler).GetFeedItemsAsync("movie").InListOrderAsync().ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(500);
		await Assert.That(asked).IsEqualTo(25);
	}

	[Test]
	[Arguments(HttpStatusCode.TooManyRequests)]
	[Arguments(HttpStatusCode.InternalServerError)]
	public async Task GetFeedItemsAsync_ForAPageItCannotRead_FailsTheLoad(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);

		await Assert
			.That(async () => await Testee(handler).GetFeedItemsAsync("discover/movie?with_genres=28").InListOrderAsync().ToListAsync())
			.Throws<HttpRequestException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPageTmdbDoesNotHave_SaysItIsNotFound()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		await Assert
			.That(async () => await Testee(handler).GetFeedItemsAsync("keyword/999999999/movie").InListOrderAsync().ToListAsync())
			.Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPageWithoutResults_FailsTheLoad()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"page":1,"total_pages":1}""");

		await Assert.That(async () => await Testee(handler).GetFeedItemsAsync("movie").InListOrderAsync().ToListAsync()).Throws<InvalidDataException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnEntryThatIsNoTitle_YieldsItUnmatched()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"page":1,"total_pages":1,"items":[{"id":287,"media_type":"person","name":"Brad Pitt"}]}""");

		var results = await Testee(handler).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(_asked).IsEmpty();
	}

	[Test]
	public async Task GetFeedItemsAsync_WithTheReadToken_ReadsTheListFromV4WithTheToken()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson("""{"page":1,"total_pages":1,"results":[{"id":550,"media_type":"movie","title":"Fight Club","release_date":"1999-10-15"}]}""");

		await Testee(handler, token: true).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(1);
		handler.Verify(request => request.PathMatches(@"^/4/list/42\?page=1$").Header("Authorization", "Bearer tmdb-token"), Times.Once);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTmdbHasNoListForTheToken_SaysTheListIsNotFound()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var drain = async () => await Testee(handler, token: true).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		await Assert.That(drain).Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTmdbHasNoListForTheKey_NamesTheReadTokenField()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var drain = async () => await Testee(handler).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		var failure = await Assert.That(drain).Throws<InvalidOperationException>();
		await Assert.That(failure!.Message).Contains("read access token");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheSecondPageFails_FailsTheLoad()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=1&")).RespondWithJson(PageOne);
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=2&")).Respond(HttpStatusCode.BadGateway);

		var drain = async () => await Testee(handler).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		await Assert.That(drain).Throws<HttpRequestException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAListInsideARun_ReportsEachPageOfItsTotal()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=1&api_key=tmdb-key$")).RespondWithJson(PageOne);
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=2&api_key=tmdb-key$")).RespondWithJson(PageTwo);
		using var spans = new PageSpans();

		await Testee(handler).GetFeedItemsAsync("42").InListOrderAsync().ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, Some.Of(2)), new SourcePage(2, Some.Of(2))], CollectionOrdering.Matching);
		await Assert.That(spans.Read).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForACategoryPageInsideARun_ReportsPagesWithoutTheSourcesTotal()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/3/tv/top_rated\?page=1&api_key=tmdb-key$"))
			.RespondWithJson("""{"page":1,"total_pages":2,"results":[{"id":1396,"name":"Breaking Bad","first_air_date":"2008-01-20"}]}""");
		handler
			.OnRequest(request => request.PathMatches(@"^/3/tv/top_rated\?page=2&api_key=tmdb-key$"))
			.RespondWithJson("""{"page":2,"total_pages":2,"results":[{"id":1399,"name":"Game of Thrones","first_air_date":"2011-04-17"}]}""");
		using var spans = new PageSpans();

		await Testee(handler).GetFeedItemsAsync("tv/top-rated").InListOrderAsync().ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, new None()), new SourcePage(2, new None())], CollectionOrdering.Matching);
		await Assert.That(spans.Read).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	private TmdbFeedService Testee(MockHttpHandler handler, bool token = false)
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key")));
		keys.GetAsync(ApiKeyProvider.TmdbReadToken, Any())
			.Returns(
				token ? Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.TmdbReadToken, "tmdb-token")) : Error.From("No API key is configured for this provider.")
			);
		var options = TmdbApis.Options;
		var client = new TmdbClient(handler.CreateClient("https://api.themoviedb.org/3"), options, keys.Object, ICurrentUser.Mock().Object);
		var media = IMediaService.Mock();
		media
			.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any())
			.Returns((entries, cancellation) => ListLoads.UnresolvedAsync(entries, _asked.Add, cancellation));
		return new TmdbFeedService(client, options, media.Object, keys.Object);
	}
}
