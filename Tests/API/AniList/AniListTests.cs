using System.Net;
using System.Text;
using API.AniList;
using API.Tests.Anime;
using API.Tests.Feeds;
using Contracts;
using Contracts.Datasets;
using Contracts.Media;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using TUnit.Mocks.Http;

namespace API.Tests.AniList;

public class AniListTests
{
	private const string PlanningList = """
		{"data":{"MediaListCollection":{"lists":[{"entries":[
		{"media":{"id":16498,"idMal":16498,"format":"TV","title":{"english":"Attack on Titan","romaji":"Shingeki no Kyojin"},"startDate":{"year":2013}}},
		{"media":{"id":199,"idMal":199,"format":"MOVIE","title":{"english":null,"romaji":"Sen to Chihiro no Kamikakushi"},"startDate":{"year":2001}}}]}]}}}
		""";

	private static readonly AniListId AttackOnTitan = AniListId.From("16498", MediaType.Show);

	private readonly List<IdentifyRequest> _asked = [];

	[Test]
	[Arguments("someone", "someone")]
	[Arguments("someone/Planning", "someone/planning")]
	[Arguments("https://anilist.co/user/someone/animelist", "someone")]
	[Arguments("https://anilist.co/user/someone/animelist/Completed", "someone/completed")]
	[Arguments(
		"https://anilist.co/search/anime?airing%20status=RELEASING&year%20range=2010&year%20range=2040",
		"search/anime?airing%20status=RELEASING&year%20range=2010&year%20range=2040"
	)]
	[Arguments("https://anilist.co/search/anime/this-season", "search/anime/this-season")]
	public async Task NormalizeAsync_ForAUserTheirListLinkOrASearch_StoresItsForm(string input, string normalized)
	{
		await Assert.That(await Feed(Mock.HttpHandler()).NormalizeAsync(input)).IsSuccess().And.IsEqualTo(normalized);
	}

	[Test]
	[Arguments("someone/watching-later")]
	[Arguments("https://myanimelist.net/animelist/someone")]
	[Arguments("https://anilist.co/search/anime?streaming%20on=Netflix")]
	[Arguments("https://anilist.co/studio/569/MAPPA")]
	public async Task NormalizeAsync_ForAnythingElse_IsAnError(string input)
	{
		await Assert.That(await Feed(Mock.HttpHandler()).NormalizeAsync(input)).IsError();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPublicList_AsksForEveryEntryByItsAniListAndMalIdWithItsTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(PlanningList);

		await Feed(handler).GetFeedItemsAsync("someone/planning", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert
			.That(_asked.Select(request => $"{string.Join(" ", request.Candidates)} {request.Hints.Map(hints => $"{hints.Title} {hints.Year}").GetValueOr("none")}"))
			.IsEquivalentTo(["AniList:Show:16498 Mal:Show:16498 Attack on Titan 2013", "AniList:Movie:199 Mal:Movie:199 Sen to Chihiro no Kamikakushi 2001"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAListHoldingALaterSeason_AsksForThatSeasonByItsShowsIds()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""
				{"data":{"MediaListCollection":{"lists":[{"entries":[
				{"media":{"id":161645,"idMal":54492,"format":"TV","title":{"english":"The Apothecary Diaries","romaji":"Kusuriya no Hitorigoto"},"startDate":{"year":2023}}},
				{"media":{"id":185407,"idMal":59130,"format":"TV","title":{"english":null,"romaji":"Kusuriya no Hitorigoto 3rd Season"},"startDate":{"year":2026}}}]}]}}}
				"""
			);
		var testee = Feed(handler, AnimeMaps.KnowingTheApothecaryDiaries());

		await testee.GetFeedItemsAsync("someone", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert
			.That(_asked.Select(request => string.Join(" ", request.Candidates)))
			.IsEquivalentTo(["AniList:Show:161645 Mal:Show:54492", string.Join(" ", AnimeMaps.ApothecaryDiariesIds)]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForASearch_ReadsPagesUntilTheLast()
	{
		var handler = Answering(out var calls, call => SearchPage(call, call == 1 ? 50 : 10, more: call == 1));

		await Feed(handler).GetFeedItemsAsync("search/anime?airing%20status=RELEASING", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(60);
		await Assert.That(calls()).IsEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForASearch_StopsAtFiveHundredTitles()
	{
		var handler = Answering(out var calls, call => SearchPage(call, 50, more: true));

		await Feed(handler).GetFeedItemsAsync("search/anime/trending", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(500);
		await Assert.That(calls()).IsEqualTo(10);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForASearchWithGenres_AsksForTheGenreCollectionFirst()
	{
		var handler = Answering(out var calls, call => call == 1 ? """{"data":{"GenreCollection":["Action","Comedy"]}}""" : SearchPage(1, 3, more: false));

		await Feed(handler).GetFeedItemsAsync("search/anime?genres=Action&genres=Isekai", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(3);
		await Assert.That(calls()).IsEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAniListRefusesTheSearch_FailsWithItsMessage()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"errors":[{"message":"Invalid sort","status":400}],"data":null}""", HttpStatusCode.BadRequest);

		var failure = await Assert
			.That(async () => await Feed(handler).GetFeedItemsAsync("search/anime?sort=NOPE", FieldNeeds.None).InListOrderAsync().ToListAsync())
			.Throws<InvalidOperationException>();

		await Assert.That(failure!.Message).IsEqualTo("AniList refused the search: Invalid sort");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenASearchPageIsAnswered429_FailsTheLoad()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);

		await Assert
			.That(async () => await Feed(handler).GetFeedItemsAsync("search/anime/popular", FieldNeeds.None).InListOrderAsync().ToListAsync())
			.Throws<RemoteUnavailableException>();
	}

	[Test]
	public async Task LookUpAsync_ForATitleTheFeedRead_AnswersWithoutAskingAniListAgain()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(PlanningList);
		var titles = Titles(handler);
		var media = IMediaService.Mock();
		media
			.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any(), Any())
			.Returns((entries, _, cancellation) => ListLoads.UnresolvedAsync(entries, _asked.Add, cancellation));
		var feed = new AniListFeedService(Client(handler), media.Object, AnimeMaps.Seasons(AnimeMaps.Unknown()), titles, FeedClock.Today());
		await feed.GetFeedItemsAsync("someone", FieldNeeds.None).InListOrderAsync().ToListAsync();

		var found = await new AniListEnricher(titles).LookUpAsync(AttackOnTitan, []);

		await Assert.That(found).IsFound();
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForATitleTheFeedReadMoreThanTenMinutesAgo_AsksAniListAgain()
	{
		var handler = Answering(out var calls, call => call == 1 ? PlanningList : """{"data":{"Page":{"media":[]}}}""");
		var now = FeedClock.Now;
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(() => now);
		var titles = new AniListTitles(Client(handler), clock.Object);
		var media = IMediaService.Mock();
		media
			.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any(), Any())
			.Returns((entries, _, cancellation) => ListLoads.UnresolvedAsync(entries, _asked.Add, cancellation));
		await new AniListFeedService(Client(handler), media.Object, AnimeMaps.Seasons(AnimeMaps.Unknown()), titles, clock.Object)
			.GetFeedItemsAsync("someone", FieldNeeds.None)
			.ToListAsync();
		now = now.AddMinutes(11);

		await new AniListEnricher(titles).LookUpAsync(AttackOnTitan, []);

		await Assert.That(calls()).IsEqualTo(2);
	}

	[Test]
	public async Task LookUpAsync_ForManyTitlesAskedTogether_AsksInBatchesOfFifty()
	{
		var handler = Answering(out var calls, _ => """{"data":{"Page":{"media":[]}}}""");
		var enricher = new AniListEnricher(Titles(handler));

		await Task.WhenAll(Enumerable.Range(1, 120).Select(id => enricher.LookUpAsync(AniListId.From($"{id}", MediaType.Show), [])));

		await Assert.That(calls()).IsEqualTo(3);
	}

	[Test]
	public async Task LookUpAsync_ForAnIdAniListLeavesOut_AnswersAnEmptyFind()
	{
		var handler = Answering(out _, _ => """{"data":{"Page":{"media":[]}}}""");

		var found = await new AniListEnricher(Titles(handler)).LookUpAsync(AttackOnTitan, []);

		await Assert.That(found).IsFound().And.Multiple(async media => await Assert.That(media.Details.Title).IsNull());
	}

	[Test]
	public async Task LookUpAsync_ForAnIdAniListLeftOutBefore_AnswersWithoutAskingAniListAgain()
	{
		var handler = Answering(out var calls, _ => """{"data":{"Page":{"media":[]}}}""");
		var enricher = new AniListEnricher(Titles(handler));
		await enricher.LookUpAsync(AttackOnTitan, []);

		await enricher.LookUpAsync(AttackOnTitan, []);

		await Assert.That(calls()).IsEqualTo(1);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPrivateList_FailsSayingItIsPrivate()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson("""{"errors":[{"message":"Private User","status":404}],"data":{"MediaListCollection":null}}""", HttpStatusCode.NotFound);

		var drain = async () => await Feed(handler).GetFeedItemsAsync("someone", FieldNeeds.None).InListOrderAsync().ToListAsync();

		var failure = await Assert.That(drain).Throws<InvalidOperationException>();
		await Assert.That(failure!.Message).Contains("private");
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnUnknownUser_SaysTheListIsNotFound()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson("""{"errors":[{"message":"User not found","status":404}],"data":{"MediaListCollection":null}}""", HttpStatusCode.NotFound);

		var drain = async () => await Feed(handler).GetFeedItemsAsync("nobody-at-all", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(drain).Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task LookUpAsync_WhenAniListIsRateLimited_SaysTheServiceIsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);

		var result = await new AniListEnricher(Titles(handler)).LookUpAsync(AttackOnTitan, []);

		await Assert.That(result).IsOfUnionType<Answer<FoundMedia>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.AniList, new None(), true));
	}

	[Test]
	public async Task LookUpAsync_ForAGraphQlError_IsFaulted()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"errors":[{"message":"Validation error","status":400}],"data":null}""", HttpStatusCode.BadRequest);

		await Assert.That(await new AniListEnricher(Titles(handler)).LookUpAsync(AttackOnTitan, [])).IsOfUnionType<Answer<FoundMedia>, Faulted>();
	}

	[Test]
	public async Task LookUpAsync_ForARecordedAnime_RatesItWithItsVotesAndPrefersTheEnglishTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "AniList", "Recorded", "attack-on-titan.json")));

		var found = await Assert.That(await new AniListEnricher(Titles(handler)).LookUpAsync(AttackOnTitan, [])).IsFound();

		var show = await Assert.That(found!.Details).IsTypeOf<ShowDetails>();
		await Assert.That(show!.Title).IsEqualTo("Attack on Titan");
		await Assert.That(show.Episodes).IsEqualTo(25);
		await Assert.That(show.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(24));
		await Assert.That(show.Ratings.All).IsEquivalentTo(new IMediaRating[] { new AniListRating(85, Some.Of(610_640)) });
		await Assert.That(found.Externals.Select(id => $"{id}")).IsEquivalentTo(["AniList:Show:16498", "Mal:Show:16498"]);
	}

	[Test]
	public async Task LookUpAsync_ForAnAnimeWithoutAnEnglishTitle_TakesTheRomajiOne()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"data":{"Page":{"media":[{"id":199,"format":"MOVIE","title":{"english":null,"romaji":"Sen to Chihiro no Kamikakushi"},"duration":125}]}}}"""
			);

		var found = await Assert.That(await new AniListEnricher(Titles(handler)).LookUpAsync(AniListId.From("199", MediaType.Movie), [])).IsFound();

		var movie = await Assert.That(found!.Details).IsTypeOf<MovieDetails>();
		await Assert.That(movie!.Title).IsEqualTo("Sen to Chihiro no Kamikakushi");
		await Assert.That(movie.Runtime).IsEqualTo(TimeSpan.FromMinutes(125));
	}

	[Test]
	public async Task Defers_TheTitleSoAnEnglishSourcesTitleWins()
	{
		await Assert.That(new AniListEnricher(Titles(Mock.HttpHandler())).Defers).IsEqualTo((MediaFields)MediaField.Title);
	}

	private static AniListClient Client(MockHttpHandler handler) => new(handler.CreateClient("https://graphql.anilist.co/"));

	private static AniListTitles Titles(MockHttpHandler handler) => new(Client(handler), FeedClock.Today());

	private static MockHttpHandler Answering(out Func<int> calls, Func<int, string> body)
	{
		var handler = Mock.HttpHandler();
		var count = 0;
		calls = () => count;
		handler
			.OnAnyRequest()
			.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(body(Interlocked.Increment(ref count)), Encoding.UTF8, "application/json"),
			});
		return handler;
	}

	private static string SearchPage(int page, int count, bool more)
	{
		var titles = Enumerable
			.Range(((page - 1) * 50) + 1, count)
			.Select(id => $$$"""{"id":{{{id}}},"format":"TV","title":{"romaji":"Anime {{{id}}}"},"startDate":{"year":2026}}""");
		var next = more ? "true" : "false";
		return "{\"data\":{\"Page\":{\"pageInfo\":{\"hasNextPage\":" + next + "},\"media\":[" + string.Join(",", titles) + "]}}}";
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPublicListInsideARun_ReportsItsOnePage()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(PlanningList);
		using var spans = new PageSpans();

		await Feed(handler).GetFeedItemsAsync("someone/planning", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, Some.Of(1))]);
		await Assert.That(spans.Read).IsEquivalentTo([1]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForASearchInsideARun_CountsItsPagesWithoutATotal()
	{
		var handler = Answering(out _, call => SearchPage(call, call == 1 ? 50 : 10, more: call == 1));
		using var spans = new PageSpans();

		await Feed(handler).GetFeedItemsAsync("search/anime?airing%20status=RELEASING", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, new None()), new SourcePage(2, new None())], CollectionOrdering.Matching);
		await Assert.That(spans.Read).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	private AniListFeedService Feed(MockHttpHandler handler) => Feed(handler, AnimeMaps.Unknown());

	private AniListFeedService Feed(MockHttpHandler handler, Mock<IAnimeIds> map)
	{
		var media = IMediaService.Mock();
		media
			.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any(), Any())
			.Returns((entries, _, cancellation) => ListLoads.UnresolvedAsync(entries, _asked.Add, cancellation));
		return new AniListFeedService(Client(handler), media.Object, AnimeMaps.Seasons(map), Titles(handler), FeedClock.Today());
	}
}
