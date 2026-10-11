using System.Net;
using API.Mdblist.Types;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistTitlesTests
{
	private static readonly ImdbId FightClub = ImdbId.From("tt0137523", MediaType.Movie);

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

	private static TUnit.Mocks.Http.MockHttpHandler Held(ManualResetEventSlim release)
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				release.Wait(TimeSpan.FromSeconds(10));
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
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
