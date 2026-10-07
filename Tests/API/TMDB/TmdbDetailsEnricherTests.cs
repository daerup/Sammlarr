using System.Net;
using API.Tmdb;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.TMDB;

public class TmdbDetailsEnricherTests
{
	private static readonly TmdbId Id = TmdbId.From("123", MediaType.Movie);

	[Test]
	public async Task Fills_RateOnlyWithTmdbsOwnRating()
	{
		await Assert.That(CreateTestee(Mock.HttpHandler()).Fills.Ratings).IsEquivalentTo([RatingSource.Tmdb]);
	}

	[Test]
	public async Task LookUpAsync_MapsTmdbRatingFromDetail()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"id\":123,\"title\":\"Example\",\"vote_average\":7.5,\"vote_count\":1000}");

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
			{
				var ratings = found.Details.Ratings;
				await Assert.That(ratings.All.Any(rating => rating.Source == RatingSource.Tmdb)).IsTrue();
				await Assert.That(ratings.All.Select(rating => rating.Source)).IsEquivalentTo(RemoteService.Tmdb.SuppliedRatingSources());
				await Assert.That(ratings.Of(RatingSource.Tmdb).Map(tmdb => tmdb.SammlarrValue)).IsSome().And.IsEqualTo(7.5f);
			});
	}

	[Test]
	[Arguments(HttpStatusCode.TooManyRequests)]
	[Arguments(HttpStatusCode.InternalServerError)]
	public async Task LookUpAsync_WhenTmdbIsUnavailable_FailsAsUnavailable(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<RemoteUnavailableException>()).IsSome());
	}

	[Test]
	public async Task LookUpAsync_WhenTmdbDoesNotKnowTheTitle_FailsAsAnAnswer()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsNone());
	}

	[Test]
	public async Task LookUpAsync_WhenTmdbSendsBlankValuesAndZeroCounts_LeavesThoseDetailsUnknown()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"id":123,"title":" ","tagline":"","overview":"  ","poster_path":null,"backdrop_path":"","release_date":"","runtime":0,"genres":[{"id":1,"name":" "}],"vote_average":0}"""
			);

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert.That(result).IsSuccess().And.Multiple(async found => await Assert.That(found.Details).IsEqualTo(new MovieDetails()));
	}

	[Test]
	public async Task LookUpAsync_ForAShow_ReadsTheFirstAirDateFirstAndTreatsZeroCountsAsUnknown()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"id":1396,"name":"Breaking Bad","first_air_date":"2008-01-20","release_date":"1999-01-01","number_of_seasons":0,"number_of_episodes":0,"episode_run_time":[0]}"""
			);

		var result = await CreateTestee(handler).LookUpAsync(TmdbId.From("1396", MediaType.Show));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
				await Assert.That(found.Details).IsEqualTo(new ShowDetails { Title = "Breaking Bad", ReleaseDate = new DateOnly(2008, 1, 20) })
			);
	}

	[Test]
	public async Task LookUpAsync_ForAnImdbId_FindsTheTmdbTitleFirstAndAsksForItsIdsWithTheDetails()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnRequest(request => request.PathMatches(".*/find/tt1375666\\?external_source=imdb_id&.*"))
			.RespondWithJson("""{"movie_results":[{"id":27205,"title":"Inception"}],"tv_results":[]}""");
		handler
			.OnRequest(request => request.PathMatches(".*/movie/27205\\?append_to_response=external_ids&.*"))
			.RespondWithJson("""{"id":27205,"title":"Inception","external_ids":{"imdb_id":"tt1375666"}}""");

		var result = await CreateTestee(handler).LookUpAsync(ImdbId.From("tt1375666", MediaType.Movie));

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Details.Title).IsEqualTo("Inception");
				IExternalMediaId[] ids = [TmdbId.From("27205", MediaType.Movie), ImdbId.From("tt1375666", MediaType.Movie)];
				await Assert.That(found.Externals).IsEquivalentTo(ids);
			});
	}

	[Test]
	public async Task LookUpAsync_ForATmdbId_SkipsTheFind()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"id":123,"title":"Example"}""");

		await CreateTestee(handler).LookUpAsync(Id);

		handler.Verify(request => request.PathMatches(".*/find/.*"), Times.Never);
		handler.Verify(request => request.PathMatches(".*/movie/123\\?append_to_response=external_ids&.*"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForAnImdbIdTmdbDoesNotKnow_Fails()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"movie_results":[],"tv_results":[]}""");

		var result = await CreateTestee(handler).LookUpAsync(ImdbId.From("tt0000000", MediaType.Movie));

		await Assert.That(result).IsError();
		handler.Verify(request => request.PathMatches(".*/movie/.*"), Times.Never);
	}

	private static TmdbDetailsEnricher CreateTestee(MockHttpHandler handler) => new(TmdbApis.Over(handler), new TmdbArtwork(TmdbApis.Options));
}
