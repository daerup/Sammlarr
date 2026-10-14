using System.Net;
using API.Http.Lanes;
using API.Mdblist.Types;
using Contracts.Providers;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistTitlesTests
{
	private static readonly ImdbId FightClub = ImdbId.From("tt0137523", MediaType.Movie);

	private const string FightClubJson = """[{"title":"Fight Club","year":1999,"ids":{"imdb":"tt0137523","tmdb":550},"ratings":[]}]""";

	[Test]
	public async Task FindAsync_AsksByTheFirstIdInMdblistsOrder()
	{
		var handler = await RecordedAsync();

		await MdblistApis.Titles(handler).FindAsync([TmdbId.From("550", MediaType.Movie), FightClub]);

		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/imdb/movie(\?.*)?$"), Times.Once);
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task FindAsync_ForAnotherIdOfAnAnsweredTitle_SendsNothingMore()
	{
		var handler = await RecordedAsync();
		var testee = MdblistApis.Titles(handler);

		await testee.FindAsync([FightClub]);
		var again = await testee.FindAsync([TmdbId.From("550", MediaType.Movie)]);

		await Assert.That(again).IsFound();
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task FindAsync_ForAnyIdTheFirstAskCarried_SendsNothingMore()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");
		var testee = MdblistApis.Titles(handler);

		await testee.FindAsync([FightClub, TvdbId.From("424242", MediaType.Movie)]);
		await testee.FindAsync([TvdbId.From("424242", MediaType.Movie)]);

		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task FindAsync_ForAShowAskedWhileAMovieRequestIsOut_SendsItInItsOwnBatch()
	{
		using var release = new ManualResetEventSlim();
		var handler = Held(release);
		var testee = MdblistApis.Titles(handler);

		var first = testee.FindAsync([FightClub]);
		var movie = testee.FindAsync([ImdbId.From("tt0133093", MediaType.Movie)]);
		var show = testee.FindAsync([ImdbId.From("tt0903747", MediaType.Show)]);
		release.Set();
		await Task.WhenAll(first, movie, show);

		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/imdb/movie(\?.*)?$"), Times.Exactly(2));
		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/imdb/show(\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task FindAsync_AfterTheFirstAskerOfATitleCancels_StillAnswersTheNextOne()
	{
		using var release = new ManualResetEventSlim();
		var handler = Held(release);
		var testee = MdblistApis.Titles(handler);
		using var leaving = new CancellationTokenSource();

		var first = testee.FindAsync([FightClub], leaving.Token);
		var next = testee.FindAsync([FightClub]);
		await leaving.CancelAsync();
		release.Set();

		await Assert.That(async () => await first).Throws<OperationCanceledException>();
		await Assert.That(await next).IsOfUnionType<Answer<MdblistTypes.Titles.Title>, NoMatch>();
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task FindAsync_WhenMdblistAnswers404ForTheBatch_IsNoMatch()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var answer = await MdblistApis.Titles(handler).FindAsync([FightClub]);

		await Assert.That(answer).IsOfUnionType<Answer<MdblistTypes.Titles.Title>, NoMatch>();
	}

	[Test]
	public async Task PrimeAsync_ForTwoHundredFiftyTitles_SendsTwoBatches()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");
		var testee = MdblistApis.Titles(handler);
		List<IReadOnlyList<IExternalMediaId>> titles =
		[
			.. Enumerable.Range(1, 250).Select(number => new IExternalMediaId[] { TmdbId.From($"{number}", MediaType.Movie) }),
		];

		await testee.PrimeAsync(titles, CancellationToken.None);
		await Task.WhenAll(titles.Select(ids => testee.FindAsync(ids)));

		handler.Verify(request => request.Method(HttpMethod.Post), Times.Exactly(2));
	}

	[Test]
	public async Task FindAsync_ForAKeyAbsentFromThePrimedGroup_FindsTheTitleThroughThePendingAskOfAnotherKey()
	{
		using var release = new ManualResetEventSlim();
		var handler = Held(release, FightClubJson);
		var testee = MdblistApis.Titles(handler);
		await testee.PrimeAsync(
			[
				[TmdbId.From("550", MediaType.Movie)],
			],
			CancellationToken.None
		);

		var asking = testee.FindAsync([FightClub, TmdbId.From("550", MediaType.Movie)]);
		release.Set();

		await Assert.That(await asking).IsFound();
		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/tmdb/movie(\?.*)?$"), Times.Once);
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task FindAsync_AfterARefusalBeforeSending_AsksAgain()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWith(_ => throw new LaneRefusedException("blocked")).Then().RespondWithJson("[]");
		var testee = MdblistApis.Titles(handler);

		await testee.FindAsync([FightClub]);
		var again = await testee.FindAsync([FightClub]);

		await Assert.That(again).IsOfUnionType<Answer<MdblistTypes.Titles.Title>, NoMatch>();
	}

	[Test]
	public async Task FindAsync_ForAMissUnderAWeakerKey_AsksOnceByTheTitlesBestKey()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");
		var testee = MdblistApis.Titles(handler);
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		await testee.PrimeAsync(
			[
				[tvdb],
			],
			CancellationToken.None
		);
		await testee.FindAsync([tvdb]);

		await testee.FindAsync([FightClub, tvdb]);
		await testee.FindAsync([FightClub, tvdb]);

		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/tvdb/movie(\?.*)?$"), Times.Once);
		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/imdb/movie(\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task FindAsync_JoiningAPrimedAskByAWeakerKeyThatMisses_AsksOnceByTheBestKey()
	{
		using var release = new ManualResetEventSlim();
		var handler = Held(release);
		var testee = MdblistApis.Titles(handler);
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		await testee.PrimeAsync(
			[
				[tvdb],
			],
			CancellationToken.None
		);

		var finding = testee.FindAsync([FightClub, tvdb]);
		release.Set();
		await finding;

		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/imdb/movie(\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task FindAsync_ByAKeyThatMissedBeforeTheBestKeyFoundTheTitle_AnswersTheTitle()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/tvdb/movie(\?.*)?$")).RespondWithJson("[]");
		handler
			.OnRequest(request => request.PathMatches(@"^/imdb/movie(\?.*)?$"))
			.RespondWithJson("""[{"title":"Fight Club","year":1999,"ids":{"imdb":"tt0137523","tvdb":81189},"ratings":[]}]""");
		var testee = MdblistApis.Titles(handler);
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		await testee.FindAsync([tvdb]);
		await testee.FindAsync([FightClub, tvdb]);

		var found = await testee.FindAsync([tvdb]);

		await Assert.That(found).IsFound();
	}

	[Test]
	public async Task FindAsync_ForTwoFindsJoiningAWeakerKeyThatMisses_AsksTheBestKeyOnce()
	{
		using var release = new ManualResetEventSlim();
		var handler = Held(release);
		var testee = MdblistApis.Titles(handler);
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		await testee.PrimeAsync(
			[
				[tvdb],
			],
			CancellationToken.None
		);

		var first = testee.FindAsync([FightClub, tvdb]);
		Task<Answer<MdblistTypes.Titles.Title>> second;
		using (LanePriority.Next(LaneClass.Title).Enter())
		{
			second = testee.FindAsync([FightClub, tvdb]);
		}

		release.Set();
		await Task.WhenAll(first, second);

		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/imdb/movie(\?.*)?$"), Times.Once);
	}

	[Test]
	public async Task FindAsync_WhenTheFirstAskerOfAnotherClassLeaves_StillAnswersTheJoiner()
	{
		using var release = new ManualResetEventSlim();
		var handler = Held(release, FightClubJson);
		var testee = MdblistApis.Titles(handler);
		using var leaving = new CancellationTokenSource();
		Task first;
		using (LanePriority.Next(LaneClass.Title).Enter())
		{
			first = testee.FindAsync([FightClub], leaving.Token);
		}

		var joiner = testee.FindAsync([FightClub]);
		await leaving.CancelAsync();
		release.Set();

		await Assert.That(await joiner).IsFound();
		await ((Task)first).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	private static TUnit.Mocks.Http.MockHttpHandler Held(ManualResetEventSlim release) => Held(release, "[]");

	private static TUnit.Mocks.Http.MockHttpHandler Held(ManualResetEventSlim release, string answer)
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				release.Wait(TimeSpan.FromSeconds(10));
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
			});
		return handler;
	}

	private static async Task<TUnit.Mocks.Http.MockHttpHandler> RecordedAsync()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));
		return handler;
	}
}
