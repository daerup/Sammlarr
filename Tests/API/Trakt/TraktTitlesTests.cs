using System.Net;
using API.Tests.Feeds;
using API.Trakt;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Trakt;

public class TraktTitlesTests
{
	private const string Inception = """
		{"title":"Inception","year":2010,"ids":{"trakt":16662,"imdb":"tt1375666","tmdb":27205},"overview":"Cobb steals secrets.",
		"released":"2010-07-16","runtime":148,"genres":["action","science-fiction"],"rating":8.43,"votes":71234}
		""";

	private static readonly ById ByAnyId = new(
		Accepts: [MediaProvider.Trakt, MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb],
		Yields: [MediaProvider.Trakt, MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb],
		Cost: 1
	);

	[Test]
	public async Task LookUpAsync_ForAnImdbId_FindsTheTitlesTraktTmdbAndImdbIdsWithHints()
	{
		var handler = Mock.HttpHandler();
		handler.OnRequest(request => request.PathMatches(@"^/movies/tt1375666\?extended=full$")).RespondWithJson(Inception);

		var result = await Converter(handler).LookUpAsync(new ByIdRequest(ByAnyId, ImdbId.From("tt1375666", MediaType.Movie)));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids.Select(id => $"{id}")).IsEquivalentTo(["Trakt:Movie:16662", "Imdb:Movie:tt1375666", "Tmdb:Movie:27205"]);
				await Assert.That(found.Hints.Map(hints => $"{hints.Title} {hints.Year}")).IsSome().And.IsEqualTo("Inception 2010");
			});
	}

	[Test]
	public async Task LookUpAsync_ForATvdbId_SearchesTraktByIt()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(@"^/search/tvdb/81189\?type=show&extended=full$"))
			.RespondWithJson(
				"""[{"type":"show","score":null,"show":{"title":"Breaking Bad","year":2008,"ids":{"trakt":1388,"imdb":"tt0903747","tmdb":1396,"tvdb":81189}}}]"""
			);

		var result = await Converter(handler).LookUpAsync(new ByIdRequest(ByAnyId, TvdbId.From("81189", MediaType.Show)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids.Select(id => $"{id}")).Contains("Tmdb:Show:1396"));
	}

	[Test]
	public async Task LookUpAsync_ForAnIdTraktDoesNotKnow_FindsNothing()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var result = await Converter(handler).LookUpAsync(new ByIdRequest(ByAnyId, ImdbId.From("tt0000000", MediaType.Movie)));

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
	}

	[Test]
	public async Task LookUpAsync_WhenTraktFails_IsAnErrorAndAsksAgainNextTime()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);
		var testee = Converter(handler);
		var request = new ByIdRequest(ByAnyId, ImdbId.From("tt1375666", MediaType.Movie));

		var first = await testee.LookUpAsync(request);
		var second = await testee.LookUpAsync(request);

		await Assert.That(first).IsError();
		await Assert.That(second).IsError();
		handler.Verify(request => request.PathMatches(".*"), Times.Exactly(2));
	}

	[Test]
	public async Task LookUpAsync_ForTheEnricher_RatesThePercentWithVotesAndFillsNoArtwork()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(Inception);
		var enricher = new TraktEnricher(Titles(handler));

		var found = await Assert.That(await enricher.LookUpAsync(TraktId.From("16662", MediaType.Movie))).IsSuccess();

		var movie = await Assert.That(found!.Details).IsTypeOf<MovieDetails>();
		await Assert.That(movie!.Ratings.All).IsEquivalentTo(new IMediaRating[] { new TraktRating(84, Some.Of(71234)) });
		await Assert.That(movie.Runtime).IsEqualTo(TimeSpan.FromMinutes(148));
		await Assert.That(movie.PosterUrl).IsNull();
		await Assert.That(enricher.Fills.Overlaps(MediaField.Poster)).IsFalse();
	}

	[Test]
	public async Task LookUpAsync_ForTheTitleTheConverterRead_AsksTraktOnce()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(Inception);
		var titles = Titles(handler);

		await new TraktIdConverter(titles).LookUpAsync(new ByIdRequest(ByAnyId, ImdbId.From("tt1375666", MediaType.Movie)));
		await new TraktEnricher(titles).LookUpAsync(TraktId.From("16662", MediaType.Movie));

		handler.Verify(request => request.PathMatches(".*"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_WhenCloudflareBlocksTheCall_SaysTraktIsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
			{
				Content = new StringContent("<html>blocked</html>", System.Text.Encoding.UTF8, "text/html"),
			});

		var result = await Converter(handler).LookUpAsync(new ByIdRequest(ByAnyId, ImdbId.From("tt1375666", MediaType.Movie)));

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error =>
				await Assert.That(error.CausedBy<RemoteUnavailableException>().Map(cause => cause.Remote)).IsSome().And.IsEqualTo(RemoteService.Trakt)
			);
	}

	[Test]
	public async Task LookUpAsync_ForTheEnricher_GivesGenresAsNames()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(Inception);

		var found = await Assert.That(await new TraktEnricher(Titles(handler)).LookUpAsync(TraktId.From("16662", MediaType.Movie))).IsSuccess();

		await Assert.That(found!.Details.Genres).IsEquivalentTo(["Action", "Science Fiction"]);
	}

	private static TraktIdConverter Converter(MockHttpHandler handler) => new(Titles(handler));

	private static TraktTitles Titles(MockHttpHandler handler)
	{
		var keys = TraktFeedServiceTests.Keys();
		return new TraktTitles(TraktFeedServiceTests.Client(handler, keys), keys.Object, FeedClock.Today());
	}
}
