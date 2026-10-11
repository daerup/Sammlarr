using System.Net;
using API.Mdblist;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistEnricherTests
{
	private static readonly ImdbId FightClub = ImdbId.From("tt0137523", MediaType.Movie);

	[Test]
	public async Task LookUpAsync_ForARecordedAnswer_ReadsEverySourceMdblistRates()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));

		var found = await Assert.That(await Testee(handler).LookUpAsync(FightClub)).IsFound();

		await Assert
			.That(found!.Details.Ratings.All)
			.IsEquivalentTo(
				new IMediaRating[]
				{
					new ImdbRating(8.8f, Some.Of(2668786)),
					new MetacriticRating(67),
					new TraktRating(87, Some.Of(56247)),
					new RottenTomatoesCriticsRating(81),
					new RottenTomatoesAudienceRating(96),
					new TmdbRating(8.4f, Some.Of(32974)),
					new LetterboxdRating(4.3f, Some.Of(6143532)),
				}
			);
	}

	[Test]
	public async Task LookUpAsync_ForARecordedAnswer_ReadsItsIdsAndThePagesItsRatingsLinkTo()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));

		var found = await Assert.That(await Testee(handler).LookUpAsync(FightClub)).IsFound();

		await Assert
			.That(found!.Externals.Select(id => $"{id}"))
			.IsEquivalentTo(["Mdblist:Movie:a5as", "Tmdb:Movie:550", "Imdb:Movie:tt0137523", "Tvdb:Movie:247", "Trakt:Movie:432"]);
		await Assert
			.That(found.Details.PageLinks.Select(link => $"{link}"))
			.IsEquivalentTo(["RottenTomatoes:Movie:m/fight_club", "Metacritic:Movie:fight-club", "Letterboxd:Movie:fight-club"]);
	}

	[Test]
	public async Task LookUpAsync_ForAShow_SkipsTheSourcesThatHaveNoScore()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("show.json"));

		var found = await Assert.That(await Testee(handler).LookUpAsync(ImdbId.From("tt0903747", MediaType.Show))).IsFound();

		await Assert.That(found!.Details.Ratings.All.Select(rating => rating.Source)).DoesNotContain(RatingSource.Letterboxd);
		await Assert.That(found.Details.Type).IsEqualTo(MediaType.Show);
	}

	[Test]
	public async Task LookUpAsync_ForATitleTheAnswerLacks_IsNoMatch()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");

		var answer = await Testee(handler).LookUpAsync(FightClub);

		await Assert.That(answer).IsOfUnionType<Answer<FoundMedia>, NoMatch>();
	}

	[Test]
	public async Task LookUpAsync_WhenMdblistIsDown_FailsAsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);

		var result = await Testee(handler).LookUpAsync(FightClub);

		await Assert.That(result).IsOfUnionType<Answer<FoundMedia>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Mdblist, new None(), true));
	}

	[Test]
	public async Task LookUpAsync_ForATitleAskedAgain_AnswersFromTheFirstAnswer()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));
		var testee = Testee(handler);

		await testee.LookUpAsync(FightClub);
		var again = await testee.LookUpAsync(FightClub);

		await Assert.That(again).IsFound();
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForTitlesAskedWhileARequestIsOut_SendsThemInOneBatch()
	{
		using var release = new ManualResetEventSlim();
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				release.Wait(TimeSpan.FromSeconds(10));
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
			});
		var testee = Testee(handler);

		var first = testee.LookUpAsync(FightClub);
		var waiting = Enumerable.Range(1, 3).Select(number => testee.LookUpAsync(ImdbId.From($"tt000000{number}", MediaType.Movie))).ToList();
		release.Set();
		await Task.WhenAll([first, .. waiting]);

		handler.Verify(request => request.Method(HttpMethod.Post), Times.Exactly(2));
	}

	[Test]
	public async Task LookUpAsync_AfterMdblistFailedToAnswer_AsksAgain()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
			.Then()
			.RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));
		var testee = Testee(handler);

		await testee.LookUpAsync(FightClub);
		var again = await testee.LookUpAsync(FightClub);

		await Assert.That(again).IsFound();
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Exactly(2));
	}

	[Test]
	public async Task LookUpAsync_ForAnAnswerThatIsNoListOfTitles_IsFaulted()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"error":"Invalid API key!"}""");

		var result = await Testee(handler).LookUpAsync(FightClub);

		await Assert.That(result).IsOfUnionType<Answer<FoundMedia>, Faulted>();
	}

	[Test]
	public async Task Fills_AreTheSourcesTheWireMapReads()
	{
		await Assert.That(MdblistRatings.ByName.Values).IsEquivalentTo(RemoteService.Mdblist.SuppliedRatingSources());
		await Assert.That(new MdblistEnricher(null!).Fills).IsEqualTo(MediaFields.Rated(RemoteService.Mdblist.SuppliedRatingSources()));
	}

	private static MdblistEnricher Testee(TUnit.Mocks.Http.MockHttpHandler handler) => new(MdblistApis.Titles(handler));
}
