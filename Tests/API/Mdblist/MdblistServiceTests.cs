using System.Net;
using System.Text;
using API.Mdblist;
using API.Tests.Feeds;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Domain.Auth;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Shared.Options;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace API.Tests.Mdblist;

public class MdblistServiceTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task GetFeedItemsAsync_WhenIdentifierReturns404_ThrowsFeedIdentifierNotFound()
	{
		const string identifier = "missing-list";
		var testee = CreateTestee(new StubHandler(HttpStatusCode.NotFound), out _);

		var act = async () =>
		{
			await foreach (var _ in testee.GetFeedItemsAsync(identifier, FieldNeeds.None)) { }
		};

		var exception = await Assert.That(act).Throws<FeedIdentifierNotFoundException>();
		await Assert.That(exception!.Identifier).IsEqualTo(identifier);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheListSpansPages_ResolvesEveryPageInOneStream()
	{
		var testee = CreateTestee(new StubHandler(HttpStatusCode.OK, pages: 2), out var media);

		var results = await testee.GetFeedItemsAsync("multi-page", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		media.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAShowHasNoIds_ReportsItUnmatchedAsAShow()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"movies":[],"shows":[{"title":"No Ids","release_year":2024}]}""");
		var testee = CreateTestee(handler, out _);

		var results = await testee.GetFeedItemsAsync("id-less", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched =>
				await Assert.That(unmatched.Details).IsEqualTo(new ShowDetails { Title = "No Ids", ReleaseDate = new DateOnly(2024, 1, 1) })
			);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemWithoutIdsHasABlankTitleAndNoYear_LeavesBothUnknown()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"movies":[{"title":" ","release_year":0}],"shows":[]}""");
		var testee = CreateTestee(handler, out _);

		var results = await testee.GetFeedItemsAsync("blank", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched.Details).IsEqualTo(new MovieDetails()));
	}

	[Test]
	public async Task NormalizeAsync_ForAListNumber_StoresItsOwnerAndSlug()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""[{"id":88434,"user_name":"snoak","name":"Trakt's Trending Shows","slug":"trakt-s-trending-shows"}]""");
		var testee = CreateTestee(handler, out _);

		var normalized = await testee.NormalizeAsync("88434");

		await Assert.That(normalized).IsSuccess().And.IsEqualTo("snoak/trakt-s-trending-shows");
		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches(@"^/lists/88434(\?.*)?$"), Times.Once);
	}

	[Test]
	[Arguments("https://mdblist.com/lists/snoak/trakt-s-trending-shows/", "snoak/trakt-s-trending-shows")]
	[Arguments("chazlarson/external/2868", "chazlarson/external/2868")]
	public async Task NormalizeAsync_ForALinkOrAPath_StoresThePathWithoutAsking(string input, string stored)
	{
		var handler = Mock.HttpHandler();
		var testee = CreateTestee(handler, out _);

		var normalized = await testee.NormalizeAsync(input);

		await Assert.That(normalized).IsSuccess().And.IsEqualTo(stored);
		handler.Verify(request => request.Method(HttpMethod.Get), Times.Never);
	}

	[Test]
	public async Task NormalizeAsync_ForANumberMdblistDoesNotKnow_SaysItHasNoSuchList()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);
		var testee = CreateTestee(handler, out _);

		var error = await Assert.That(await testee.NormalizeAsync("99999999")).IsError();

		await Assert.That(error!.Reason).IsEqualTo("MDBList has no list 99999999.");
	}

	[Test]
	public async Task NormalizeAsync_WhenMdblistDoesNotAnswer_AsksForTheLinkAndBlamesMdblist()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);
		var testee = CreateTestee(handler, out _);

		var error = await Assert.That(await testee.NormalizeAsync("88434")).IsError();

		await Assert.That(error!.Reason).Contains("Paste the list's link instead");
		await Assert.That(error.CausedBy<RemoteUnavailableException>()).IsSome();
	}

	[Test]
	public async Task NormalizeAsync_WhenMdblistCannotBeReached_AsksForTheLinkAndBlamesMdblist()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("No such host is known."));
		var testee = CreateTestee(handler, out _);

		var error = await Assert.That(await testee.NormalizeAsync("88434")).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsSome();
	}

	[Test]
	public async Task NormalizeAsync_ForSomethingThatIsNoList_RefusesWithoutAsking()
	{
		var handler = Mock.HttpHandler();
		var testee = CreateTestee(handler, out _);

		var error = await Assert.That(await testee.NormalizeAsync("top-250-movies")).IsError();

		await Assert.That(error!.Reason).IsEqualTo("Paste the list's link from mdblist.com, its owner/list, or its number.");
		handler.Verify(request => request.Method(HttpMethod.Get), Times.Never);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAnExternalList_PagesThroughItsExternalItems()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"movies":[],"shows":[]}""");
		var testee = CreateTestee(handler, out _);

		await testee.GetFeedItemsAsync("chazlarson/external/2868", FieldNeeds.None).InListOrderAsync().ToListAsync();

		handler.Verify(request => request.Method(HttpMethod.Get).PathMatches(@"^/external/lists/2868/items(\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task GetFeedItemsAsync_InsideARun_ReportsEachPageWithoutATotal()
	{
		var testee = CreateTestee(new StubHandler(HttpStatusCode.OK, pages: 2), out _);
		using var spans = new PageSpans();

		await testee.GetFeedItemsAsync("multi-page", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(spans.Pages).IsEquivalentTo([new SourcePage(1, new None()), new SourcePage(2, new None())], CollectionOrdering.Matching);
		await Assert.That(spans.Read).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	private static MdblistService CreateTestee(HttpMessageHandler handler, out Mock<IMediaService> media)
	{
		var apiKeyStore = IApiKeyService.Mock();
		IApiKey apiKey = new TestApiKey(ApiKeyProvider.Mdblist, "test-key");
		apiKeyStore.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of(apiKey));
		var options = new MdblistOptions { BaseUrl = BaseUrl };
		var client = new MdblistClient(new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) }, options, apiKeyStore.Object, ICurrentUser.Mock().Object);
		media = IMediaService.Mock();
		media.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any(), Any()).Returns((entries, _, token) => ListLoads.UnresolvedAsync(entries, _ => { }, token));
		return new MdblistService(client, media.Object, apiKeyStore.Object);
	}

	private sealed class StubHandler(HttpStatusCode status, int pages = 1) : HttpMessageHandler
	{
		private int _calls;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var index = Interlocked.Increment(ref _calls);
			var response = new HttpResponseMessage(status);
			if (status is not HttpStatusCode.OK)
			{
				return Task.FromResult(response);
			}

			response.Content = new StringContent(Page($"page-{index}"), Encoding.UTF8, "application/json");
			if (index < pages)
			{
				response.Headers.Add("X-Has-More", "true");
			}
			return Task.FromResult(response);
		}

		private static string Page(string title) =>
			$$"""{"movies":[{"id":1,"ids":{"imdb":null,"tmdb":{{title.GetHashCode(StringComparison.Ordinal) & 0xffff}},"tvdb":null,"mdblist":null,"mal":null},"rank":1,"title":"{{title}}","release_year":2020}],"shows":[]}""";
	}
}
