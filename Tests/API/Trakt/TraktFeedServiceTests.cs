using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using API.Trakt;
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

namespace API.Tests.Trakt;

public class TraktFeedServiceTests
{
	private readonly List<IdentifyRequest> _asked = [];

	[Test]
	[Arguments("ana", "users/ana/watchlist")]
	[Arguments("https://trakt.tv/users/ana", "users/ana/watchlist")]
	[Arguments("https://trakt.tv/users/ana/watchlist?sort=rank", "users/ana/watchlist")]
	[Arguments("https://app.trakt.tv/users/ana/watchlist", "users/ana/watchlist")]
	[Arguments("trakt.tv/users/ana/lists/marvel", "users/ana/lists/marvel")]
	[Arguments("https://trakt.tv/lists/2142753", "lists/2142753")]
	[Arguments("https://trakt.tv/movies/trending", "movies/trending")]
	[Arguments("https://app.trakt.tv/discover/popular?mode=show", "shows/popular")]
	[Arguments("https://app.trakt.tv/discover/trending", "discover/trending")]
	[Arguments("https://trakt.tv/shows/anticipated", "shows/anticipated")]
	public async Task NormalizeAsync_ForEveryForm_StoresThePathOnTrakt(string input, string stored)
	{
		await Assert.That(await Testee(Mock.HttpHandler()).NormalizeAsync(input)).IsSuccess().And.IsEqualTo(stored);
	}

	[Test]
	[Arguments("https://trakt.tv/movies/boxoffice")]
	[Arguments("https://trakt.tv/movies/watched/weekly")]
	[Arguments("https://trakt.tv/people/keanu-reeves")]
	[Arguments("https://anilist.co/user/ana")]
	public async Task NormalizeAsync_ForAPageItCannotFollow_IsAnError(string input)
	{
		await Assert.That(await Testee(Mock.HttpHandler()).NormalizeAsync(input)).IsError();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAWatchlistAcrossTwoPages_AsksForEveryTitleByEveryIdItCarries()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/users/ana/watchlist\?page=1&limit=250$"))
			.RespondWith(_ =>
				Page("""[{"type":"movie","movie":{"title":"Inception","year":2010,"ids":{"trakt":16662,"imdb":"tt1375666","tmdb":27205}}}]""", pages: 2)
			);
		handler
			.OnRequest(request => request.PathMatches(@"^/users/ana/watchlist\?page=2&limit=250$"))
			.RespondWith(_ =>
				Page(
					"""[{"type":"season","show":{"title":"Breaking Bad","year":2008,"ids":{"trakt":1388,"imdb":"tt0903747","tmdb":1396,"tvdb":81189}},"season":{"number":2}}]""",
					pages: 2
				)
			);

		await Testee(handler).GetFeedItemsAsync("users/ana/watchlist").ToListAsync();

		await Assert
			.That(_asked.Select(request => string.Join(" ", request.Candidates)))
			.IsEquivalentTo(["Trakt:Movie:16662 Imdb:Movie:tt1375666 Tmdb:Movie:27205", "Trakt:Show:1388 Imdb:Show:tt0903747 Tmdb:Show:1396 Tvdb:Show:81189"]);
		handler.Verify(request => request.Header("trakt-api-key", "trakt-client-id").Header("trakt-api-version", "2"), Times.Exactly(2));
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAList_AsksForItsTitlesOnly()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/users/ana/lists/marvel/items/movie,show,season,episode\?page=1&limit=250$"))
			.RespondWith(_ => Page("""[{"type":"movie","movie":{"title":"Iron Man","year":2008,"ids":{"trakt":1,"tmdb":1726}}}]""", pages: 1));

		await Testee(handler).GetFeedItemsAsync("users/ana/lists/marvel").ToListAsync();

		await Assert.That(_asked.Select(request => string.Join(" ", request.Candidates))).IsEquivalentTo(["Trakt:Movie:1 Tmdb:Movie:1726"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAChart_StopsAtFiveHundredTitlesAndTypesBareTitlesByTheChart()
	{
		var handler = Mock.HttpHandler();
		var calls = 0;
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				var page = Interlocked.Increment(ref calls);
				var titles = Enumerable.Range(((page - 1) * 250) + 1, 250).Select(id => $$$"""{"title":"Show {{{id}}}","year":2026,"ids":{"trakt":{{{id}}}}}""");
				return Page("[" + string.Join(",", titles) + "]", pages: 10);
			});

		await Testee(handler).GetFeedItemsAsync("shows/popular").ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(500);
		await Assert.That(_asked[0].Candidates.Single()).IsEqualTo<IExternalMediaId>(TraktId.From("1", MediaType.Show));
		await Assert.That(calls).IsEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForADiscoverChartWithoutAMode_ReadsTheMovieAndTheShowChart()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/movies/popular\?page=1&limit=250$"))
			.RespondWith(_ => Page("""[{"title":"Dune","year":2021,"ids":{"trakt":287071}}]""", pages: 1));
		handler
			.OnRequest(request => request.PathMatches(@"^/shows/popular\?page=1&limit=250$"))
			.RespondWith(_ => Page("""[{"title":"Severance","year":2022,"ids":{"trakt":154997}}]""", pages: 1));

		await Testee(handler).GetFeedItemsAsync("discover/popular").ToListAsync();

		await Assert.That(_asked.Select(request => string.Join(" ", request.Candidates))).IsEquivalentTo(["Trakt:Movie:287071", "Trakt:Show:154997"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnEpisodeAndATrendingEntry_AsksForTheirShowAndMovie()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/users/ana/watchlist"))
			.RespondWith(_ =>
				Page(
					"""[{"type":"episode","episode":{"season":1,"number":2},"show":{"title":"Severance","year":2022,"ids":{"trakt":154997,"tmdb":95396,"tvdb":371980,"imdb":null}}}]""",
					pages: 1
				)
			);
		handler
			.OnRequest(request => request.PathMatches(@"^/movies/trending"))
			.RespondWith(_ => Page("""[{"watchers":31,"movie":{"title":"Dune","year":2021,"ids":{"trakt":287071,"tmdb":438631}}}]""", pages: 1));

		await Testee(handler).GetFeedItemsAsync("users/ana/watchlist").ToListAsync();
		await Testee(handler).GetFeedItemsAsync("movies/trending").ToListAsync();

		await Assert
			.That(_asked.Select(request => string.Join(" ", request.Candidates)))
			.IsEquivalentTo(["Trakt:Show:154997 Tmdb:Show:95396 Tvdb:Show:371980", "Trakt:Movie:287071 Tmdb:Movie:438631"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPrivateUser_FailsSayingTheProfileIsPrivate()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
				response.Headers.Add("X-Private-User", "true");
				return response;
			});

		var failure = await Assert
			.That(async () => await Testee(handler).GetFeedItemsAsync("users/ana/watchlist").ToListAsync())
			.Throws<InvalidOperationException>();

		await Assert.That(failure!.Message).Contains("private");
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnUnknownUser_SaysTheListIsNotFound()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		await Assert.That(async () => await Testee(handler).GetFeedItemsAsync("users/nobody/watchlist").ToListAsync()).Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForTraktsOwnForbidden_ReportsTheClientIdRejected()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"error":"invalid api key"}""", HttpStatusCode.Forbidden);
		var keys = Keys();

		await Assert.That(async () => await Testee(handler, keys).GetFeedItemsAsync("movies/trending").ToListAsync()).Throws<InvalidOperationException>();

		keys.ReportRejected(ApiKeyProvider.Trakt).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForCloudflaresForbiddenPageOr429_SaysTraktIsUnavailable()
	{
		var blocked = Mock.HttpHandler();
		blocked
			.OnAnyRequest()
			.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("<html>blocked</html>", Encoding.UTF8, "text/html") });
		var limited = Mock.HttpHandler();
		limited.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);
		var keys = Keys();

		await Assert.That(async () => await Testee(blocked, keys).GetFeedItemsAsync("movies/trending").ToListAsync()).Throws<RemoteUnavailableException>();
		await Assert.That(async () => await Testee(limited, keys).GetFeedItemsAsync("movies/trending").ToListAsync()).Throws<RemoteUnavailableException>();
		keys.ReportRejected(ApiKeyProvider.Trakt).WasNeverCalled();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnEntryThatIsNoTitle_FailsTheLoad()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWith(_ => Page("""[{"type":"person","person":{"name":"Keanu Reeves"}}]""", pages: 1));

		await Assert.That(async () => await Testee(handler).GetFeedItemsAsync("users/ana/watchlist").ToListAsync()).Throws<InvalidDataException>();
	}

	internal static HttpResponseMessage Page(string body, int pages)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
		response.Headers.Add("X-Pagination-Page-Count", $"{pages}");
		return response;
	}

	internal static Mock<IApiKeyService> Keys()
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Trakt, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Trakt, "trakt-client-id")));
		return keys;
	}

	internal static TraktClient Client(MockHttpHandler handler, Mock<IApiKeyService> keys) =>
		new(handler.CreateClient("https://api.trakt.tv/"), keys.Object, ICurrentUser.Mock().Object);

	[Test]
	public async Task GetFeedItemsAsync_ForAWatchlistInsideARun_ReportsEachPageOfItsTotal()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/users/ana/watchlist\?page=1&limit=250$"))
			.RespondWith(_ => Page("""[{"type":"movie","movie":{"title":"Inception","year":2010,"ids":{"trakt":16662}}}]""", pages: 2));
		handler
			.OnRequest(request => request.PathMatches(@"^/users/ana/watchlist\?page=2&limit=250$"))
			.RespondWith(_ => Page("""[{"type":"movie","movie":{"title":"Heat","year":1995,"ids":{"trakt":1374}}}]""", pages: 2));
		using var spans = new PageSpans();

		await Testee(handler).GetFeedItemsAsync("users/ana/watchlist").ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, Some.Of(2)), new SourcePage(2, Some.Of(2))], CollectionOrdering.Matching);
		await Assert.That(spans.Read).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForADiscoverChartInsideARun_CountsPagesAcrossBothCharts()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/movies/popular\?page=1&limit=250$"))
			.RespondWith(_ => Page("""[{"title":"Dune","year":2021,"ids":{"trakt":287071}}]""", pages: 1));
		handler
			.OnRequest(request => request.PathMatches(@"^/shows/popular\?page=1&limit=250$"))
			.RespondWith(_ => Page("""[{"title":"Severance","year":2022,"ids":{"trakt":154997}}]""", pages: 1));
		using var spans = new PageSpans();

		await Testee(handler).GetFeedItemsAsync("discover/popular").ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, new None()), new SourcePage(2, new None())], CollectionOrdering.Matching);
		await Assert.That(spans.Read).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	private TraktFeedService Testee(MockHttpHandler handler) => Testee(handler, Keys());

	private TraktFeedService Testee(MockHttpHandler handler, Mock<IApiKeyService> keys)
	{
		var media = IMediaService.Mock();
		media.IdentifyWithDetailsAsync(Any<IAsyncEnumerable<IdentifyRequest>>(), Any()).Returns((requests, cancellation) => Identify(requests, cancellation));
		return new TraktFeedService(Client(handler, keys), media.Object, keys.Object);
	}

	private async IAsyncEnumerable<MediaMatchResult> Identify(
		IAsyncEnumerable<IdentifyRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		await foreach (var request in requests.WithCancellation(cancellationToken))
		{
			_asked.Add(request);
			yield return new UnmatchedMedia([.. request.Candidates], MediaDetails.Empty(request.Candidates[0].Type));
		}
	}
}
