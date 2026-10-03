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
				var rating = found.Details.Rating;
				await Assert.That(rating.Sources.Any(source => source.Source == MediaProvider.Tmdb)).IsTrue();
				await Assert.That(rating.Average).IsEqualTo(7.5f);
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

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsTypeOf<ProviderUnavailableException>());
	}

	[Test]
	public async Task LookUpAsync_WhenTmdbDoesNotKnowTheTitle_FailsAsAnAnswer()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsNull());
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

	private static TmdbDetailsEnricher CreateTestee(MockHttpHandler handler) => new(TmdbApis.Over(handler), new TmdbArtwork(TmdbApis.Options));
}
