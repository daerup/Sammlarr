using System.Net;
using System.Runtime.CompilerServices;
using API.Tmdb;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
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

		await Testee(handler).GetFeedItemsAsync("42").ToListAsync();

		await Assert
			.That(_asked.Select(request => $"{request.Candidates.Single()} {request.Hints.Map(hints => $"{hints.Title} {hints.Year}").GetValueOr("none")}"))
			.IsEquivalentTo(["Tmdb:Movie:550 Fight Club 1999", "Tmdb:Show:1396 Breaking Bad 2008", "Tmdb:Movie:27205 Inception 2010"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnEntryThatIsNoTitle_YieldsItUnmatched()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"page":1,"total_pages":1,"items":[{"id":287,"media_type":"person","name":"Brad Pitt"}]}""");

		var results = await Testee(handler).GetFeedItemsAsync("42").ToListAsync();

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

		await Testee(handler, token: true).GetFeedItemsAsync("42").ToListAsync();

		await Assert.That(_asked).Count().IsEqualTo(1);
		handler.Verify(request => request.PathMatches(@"^/4/list/42\?page=1$").Header("Authorization", "Bearer tmdb-token"), Times.Once);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTmdbHasNoListForTheToken_SaysTheListIsNotFound()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var drain = async () => await Testee(handler, token: true).GetFeedItemsAsync("42").ToListAsync();

		await Assert.That(drain).Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTmdbHasNoListForTheKey_NamesTheReadTokenField()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var drain = async () => await Testee(handler).GetFeedItemsAsync("42").ToListAsync();

		var failure = await Assert.That(drain).Throws<InvalidOperationException>();
		await Assert.That(failure!.Message).Contains("read access token");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheSecondPageFails_FailsTheLoad()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=1&")).RespondWithJson(PageOne);
		handler.OnRequest(request => request.PathMatches(@"^/3/list/42\?page=2&")).Respond(HttpStatusCode.BadGateway);

		var drain = async () => await Testee(handler).GetFeedItemsAsync("42").ToListAsync();

		await Assert.That(drain).Throws<HttpRequestException>();
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
		media.IdentifyWithDetailsAsync(Any<IAsyncEnumerable<IdentifyRequest>>(), Any()).Returns((requests, cancellation) => Identify(requests, cancellation));
		return new TmdbFeedService(client, options, media.Object, keys.Object);
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
