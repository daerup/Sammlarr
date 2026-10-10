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
	public async Task LookUpAsync_ForATitleTheAnswerLacks_IsAnEmptyAnswerOfItsType()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");

		var found = await Assert.That(await Testee(handler).LookUpAsync(FightClub)).IsFound();

		await Assert.That(found!.Details.Ratings.All).IsEmpty();
		await Assert.That(found.Details.Type).IsEqualTo(MediaType.Movie);
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
	public async Task LookUpAsync_ForSeveralTitlesAtOnce_AsksInOneBatch()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));
		var testee = Testee(handler);

		var answers = await Task.WhenAll(testee.LookUpAsync(FightClub), testee.LookUpAsync(ImdbId.From("tt0133093", MediaType.Movie)));

		await Assert
			.That(answers.AsEnumerable())
			.AreOfUnionType<IEnumerable<Answer<FoundMedia>>, Answer<FoundMedia>>(typeof(Found<FoundMedia>), typeof(Found<FoundMedia>));
		handler.Verify(request => request.PathMatches(@"^/imdb/movie(\?.*)?$"), Times.Once);
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
	public async Task LookUpAsync_ByAnotherIdOfAPrefetchedTitle_AnswersFromThePrefetch()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await MdblistApis.RecordedAsync("movies.json"));
		var titles = MdblistApis.Titles(handler);
		titles.Prefetch([
			[MdblistId.From("a5as", MediaType.Movie), FightClub],
		]);

		var found = await Assert.That(await new MdblistEnricher(titles).LookUpAsync(FightClub)).IsFound();

		await Assert.That(found!.Details.Ratings.All).IsNotEmpty();
		handler.Verify(request => request.Method(HttpMethod.Post).PathMatches(@"^/mdblist/movie(\?.*)?$"), Times.Once);
		handler.Verify(request => request.Method(HttpMethod.Post), Times.Once);
	}

	[Test]
	public async Task Prefetch_ForMoreTitlesThanOneBatchHolds_AsksTwoHundredAtATime()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");
		var titles = MdblistApis.Titles(handler);
		IReadOnlyCollection<IExternalMediaId>[] list =
		[
			.. Enumerable.Range(1, 250).Select(number => new[] { (IExternalMediaId)TmdbId.From($"{number}", MediaType.Movie) }),
		];

		titles.Prefetch(list);
		await titles.FindAsync(TmdbId.From("1", MediaType.Movie));
		await titles.FindAsync(TmdbId.From("250", MediaType.Movie));

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
	public async Task LookUpAsync_ForAnErrorBodyWithStatus200_IsFaultedAndLogged()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"error":"Invalid API key!"}""");
		var logger = new CountingLogger<MdblistTitles>();

		var result = await new MdblistEnricher(MdblistApis.Titles(handler, logger: logger)).LookUpAsync(FightClub);

		await Assert.That(result).IsOfUnionType<Answer<FoundMedia>, Faulted>();
		await Assert.That(logger.Warnings).IsEqualTo(1);
	}

	[Test]
	public async Task LookUpAsync_AtTheQuotaReserve_IsUnavailableUntilTheResetAndAsksNothing()
	{
		var budget = MdblistApis.Budget();
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", "120");
		answered.Headers.Add("x-ratelimit-reset", $"{DateTimeOffset.UnixEpoch.AddDays(2).ToUnixTimeSeconds()}");
		budget.Record("mdblist-key", answered.Headers);
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("[]");

		var result = await Testee(handler, budget).LookUpAsync(FightClub);

		await Assert
			.That(result)
			.IsOfUnionType<Answer<FoundMedia>, Unavailable>()
			.And.IsEqualTo(new Unavailable(RemoteService.Mdblist, Some.Of(DateTimeOffset.UnixEpoch.AddDays(2)), false));
		handler.Verify(request => request.PathMatches(".*"), Times.Never);
	}

	[Test]
	public async Task Fills_AreTheSourcesTheWireMapReads()
	{
		await Assert.That(MdblistRatings.ByName.Values).IsEquivalentTo(RemoteService.Mdblist.SuppliedRatingSources());
		await Assert.That(new MdblistEnricher(null!).Fills).IsEqualTo(MediaFields.Rated(RemoteService.Mdblist.SuppliedRatingSources()));
	}

	private static MdblistEnricher Testee(TUnit.Mocks.Http.MockHttpHandler handler, MdblistBudget? budget = null) => new(MdblistApis.Titles(handler, budget));
}
