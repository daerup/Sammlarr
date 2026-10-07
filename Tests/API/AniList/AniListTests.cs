using System.Net;
using API.AniList;
using Contracts.Media;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
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
	public async Task NormalizeAsync_ForAUserOrTheirListLink_StoresTheUserAndStatus(string input, string normalized)
	{
		await Assert.That(await Feed(Mock.HttpHandler()).NormalizeAsync(input)).IsSuccess().And.IsEqualTo(normalized);
	}

	[Test]
	[Arguments("someone/watching-later")]
	[Arguments("https://myanimelist.net/animelist/someone")]
	public async Task NormalizeAsync_ForAnythingElse_IsAnError(string input)
	{
		await Assert.That(await Feed(Mock.HttpHandler()).NormalizeAsync(input)).IsError();
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPublicList_AsksForEveryEntryByItsAniListAndMalIdWithItsTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(PlanningList);

		await Feed(handler).GetFeedItemsAsync("someone/planning").ToListAsync();

		await Assert
			.That(_asked.Select(request => $"{string.Join(" ", request.Candidates)} {request.Hints.Map(hints => $"{hints.Title} {hints.Year}").GetValueOr("none")}"))
			.IsEquivalentTo(["AniList:Show:16498 Mal:Show:16498 Attack on Titan 2013", "AniList:Movie:199 Mal:Movie:199 Sen to Chihiro no Kamikakushi 2001"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_ForAPrivateList_FailsSayingItIsPrivate()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson("""{"errors":[{"message":"Private User","status":404}],"data":{"MediaListCollection":null}}""", HttpStatusCode.NotFound);

		var drain = async () => await Feed(handler).GetFeedItemsAsync("someone").ToListAsync();

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

		var drain = async () => await Feed(handler).GetFeedItemsAsync("nobody-at-all").ToListAsync();

		await Assert.That(drain).Throws<FeedIdentifierNotFoundException>();
	}

	[Test]
	public async Task LookUpAsync_WhenAniListIsRateLimited_SaysTheServiceIsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);

		var result = await new AniListEnricher(Client(handler)).LookUpAsync(AttackOnTitan);

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error =>
				await Assert.That(error.CausedBy<RemoteUnavailableException>().Map(cause => cause.Remote)).IsSome().And.IsEqualTo(RemoteService.AniList)
			);
	}

	[Test]
	public async Task LookUpAsync_ForAGraphQlError_IsAnError()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"errors":[{"message":"Validation error","status":400}],"data":null}""", HttpStatusCode.BadRequest);

		await Assert.That(await new AniListEnricher(Client(handler)).LookUpAsync(AttackOnTitan)).IsError();
	}

	[Test]
	public async Task LookUpAsync_ForARecordedAnime_RatesItWithItsVotesAndPrefersTheEnglishTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "AniList", "Recorded", "attack-on-titan.json")));

		var found = await Assert.That(await new AniListEnricher(Client(handler)).LookUpAsync(AttackOnTitan)).IsSuccess();

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
			.RespondWithJson("""{"data":{"Media":{"id":199,"format":"MOVIE","title":{"english":null,"romaji":"Sen to Chihiro no Kamikakushi"},"duration":125}}}""");

		var found = await Assert.That(await new AniListEnricher(Client(handler)).LookUpAsync(AniListId.From("199", MediaType.Movie))).IsSuccess();

		var movie = await Assert.That(found!.Details).IsTypeOf<MovieDetails>();
		await Assert.That(movie!.Title).IsEqualTo("Sen to Chihiro no Kamikakushi");
		await Assert.That(movie.Runtime).IsEqualTo(TimeSpan.FromMinutes(125));
	}

	[Test]
	public async Task Defers_TheTitleSoAnEnglishSourcesTitleWins()
	{
		await Assert.That(new AniListEnricher(Client(Mock.HttpHandler())).Defers).IsEqualTo((MediaFields)MediaField.Title);
	}

	private static AniListClient Client(MockHttpHandler handler) => new(handler.CreateClient("https://graphql.anilist.co/"));

	private AniListFeedService Feed(MockHttpHandler handler)
	{
		var media = IMediaService.Mock();
		media.IdentifyWithDetailsAsync(Any<IAsyncEnumerable<IdentifyRequest>>(), Any()).Returns((requests, cancellation) => Identify(requests, cancellation));
		return new AniListFeedService(Client(handler), media.Object);
	}

	private async IAsyncEnumerable<MediaMatchResult> Identify(
		IAsyncEnumerable<IdentifyRequest> requests,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		await foreach (var request in requests.WithCancellation(cancellationToken))
		{
			_asked.Add(request);
			yield return new UnmatchedMedia([.. request.Candidates], MediaDetails.Empty(request.Candidates[0].Type));
		}
	}
}
