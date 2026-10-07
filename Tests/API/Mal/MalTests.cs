using System.Net;
using System.Runtime.CompilerServices;
using API.Mal;
using API.Tests.Anime;
using Contracts.Auth;
using Contracts.Datasets;
using Contracts.Identity;
using Contracts.Media;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Mal;

public class MalTests
{
	private const string PageOne = """
		{"data":[{"node":{"id":16498,"title":"Shingeki no Kyojin","alternative_titles":{"en":"Attack on Titan"},"media_type":"tv","start_date":"2013-04-07"}}],
		"paging":{"next":"https://api.myanimelist.net/v2/users/someone/animelist?offset=1&fields=list_status&limit=1000"}}
		""";

	private const string PageTwo = """
		{"data":[{"node":{"id":199,"title":"Sen to Chihiro no Kamikakushi","alternative_titles":{"en":""},"media_type":"movie","start_date":"2001-07-20"}}],"paging":{}}
		""";

	private const string FullAnime = """
		{"id":16498,"title":"Shingeki no Kyojin","alternative_titles":{"en":"Attack on Titan"},"media_type":"tv","start_date":"2013-04-07",
		"synopsis":"Centuries ago, mankind was slaughtered to near extinction.","main_picture":{"medium":"https://cdn.myanimelist.net/images/anime/10/47347.jpg","large":"https://cdn.myanimelist.net/images/anime/10/47347l.jpg"},
		"genres":[{"id":1,"name":"Action"}],"num_episodes":25,"average_episode_duration":1440,"mean":8.54,"num_scoring_users":2826539}
		""";

	private readonly List<IdentifyRequest> _asked = [];

	[Test]
	[Arguments("someone", "someone")]
	[Arguments("https://myanimelist.net/animelist/someone", "someone")]
	[Arguments("https://myanimelist.net/animelist/someone?status=2", "someone")]
	public async Task NormalizeAsync_ForAUserOrTheirListLink_StoresTheUser(string input, string user)
	{
		await Assert.That(await Feed(Mock.HttpHandler()).NormalizeAsync(input)).IsSuccess().And.IsEqualTo(user);
	}

	[Test]
	public async Task NormalizeAsync_ForAnotherSitesLink_IsAnError()
	{
		await Assert.That(await Feed(Mock.HttpHandler()).NormalizeAsync("https://anilist.co/user/someone/animelist")).IsError();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForTwoPages_AsksForEveryEntryWithTheClientIdAndItsEnglishOrOwnTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/v2/users/someone/animelist\?fields=")).RespondWithJson(PageOne);
		handler.OnRequest(request => request.PathMatches(".*offset=1.*")).RespondWithJson(PageTwo);

		await Feed(handler).GetFeedItemsAsync("someone").ToListAsync();

		await Assert
			.That(_asked.Select(request => $"{request.Candidates.Single()} {request.Hints.Map(hints => $"{hints.Title} {hints.Year}").GetValueOr("none")}"))
			.IsEquivalentTo(["Mal:Show:16498 Attack on Titan 2013", "Mal:Movie:199 Sen to Chihiro no Kamikakushi 2001"]);
		handler.Verify(request => request.Header("X-MAL-CLIENT-ID", "mal-client-id"), Times.Exactly(2));
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAListHoldingALaterSeason_AsksForThatSeasonByItsShowsIds()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""
				{"data":[{"node":{"id":54492,"title":"Kusuriya no Hitorigoto","media_type":"tv","start_date":"2023-10-22"}},{"node":{"id":59130,"title":"Kusuriya no Hitorigoto 3rd Season","media_type":"tv","start_date":"2026-10-01"}}],"paging":{}}
				"""
			);

		await Feed(handler, AnimeMaps.KnowingTheApothecaryDiaries()).GetFeedItemsAsync("someone").ToListAsync();

		await Assert
			.That(_asked.Select(request => string.Join(" ", request.Candidates)))
			.IsEquivalentTo(["Mal:Show:54492", string.Join(" ", AnimeMaps.ApothecaryDiariesIds)]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPrivateList_FailsSayingItIsPrivate()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.Forbidden);

		var failure = await Assert.That(async () => await Feed(handler).GetFeedItemsAsync("someone").ToListAsync()).Throws<InvalidOperationException>();

		await Assert.That(failure!.Message).Contains("private");
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnUnknownUser_SaysTheListIsNotFound()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		await Assert.That(async () => await Feed(handler).GetFeedItemsAsync("nobody").ToListAsync()).Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task LookUpAsync_WhenMalRejectsTheClientId_ReportsTheIdRejected()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);
		var keys = Keys();

		await new MalEnricher(Client(handler, keys), keys.Object).LookUpAsync(MalId.From("16498", MediaType.Show));

		keys.ReportRejected(ApiKeyProvider.Mal).WasCalled(Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForAnAnime_RatesItWithItsVotesAndPrefersTheEnglishTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(FullAnime);
		var keys = Keys();

		var found = await Assert.That(await new MalEnricher(Client(handler, keys), keys.Object).LookUpAsync(MalId.From("16498", MediaType.Show))).IsSuccess();

		var show = await Assert.That(found!.Details).IsTypeOf<ShowDetails>();
		await Assert.That(show!.Title).IsEqualTo("Attack on Titan");
		await Assert.That(show.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(24));
		await Assert.That(show.Ratings.All).IsEquivalentTo(new IMediaRating[] { new MyAnimeListRating(8.54f, Some.Of(2_826_539)) });
	}

	[Test]
	public async Task LookUpAsync_ForAnAnimeWithoutAnEnglishTitle_TakesItsOwnTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"id":199,"title":"Sen to Chihiro no Kamikakushi","alternative_titles":{"en":""},"media_type":"movie"}""");
		var keys = Keys();

		var found = await Assert.That(await new MalEnricher(Client(handler, keys), keys.Object).LookUpAsync(MalId.From("199", MediaType.Movie))).IsSuccess();

		await Assert.That(found!.Details.Title).IsEqualTo("Sen to Chihiro no Kamikakushi");
	}

	[Test]
	public async Task Fills_TheMyAnimeListRatingAsItsSecondSupplier()
	{
		await Assert.That(RatingSource.MyAnimeList.SuppliedBy()).Contains(RemoteService.Mal).And.Contains(RemoteService.Mdblist);
	}

	private static Mock<IApiKeyService> Keys()
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mal, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Mal, "mal-client-id")));
		return keys;
	}

	private static MalClient Client(MockHttpHandler handler, Mock<IApiKeyService> keys) =>
		new(handler.CreateClient("https://api.myanimelist.net/v2/"), keys.Object, ICurrentUser.Mock().Object);

	private MalFeedService Feed(MockHttpHandler handler) => Feed(handler, AnimeMaps.Unknown());

	private MalFeedService Feed(MockHttpHandler handler, Mock<IAnimeIds> map)
	{
		var keys = Keys();
		var media = IMediaService.Mock();
		media.IdentifyWithDetailsAsync(Any<IAsyncEnumerable<IdentifyRequest>>(), Any()).Returns((requests, cancellation) => Identify(requests, cancellation));
		return new MalFeedService(Client(handler, keys), media.Object, keys.Object, AnimeMaps.Seasons(map));
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
