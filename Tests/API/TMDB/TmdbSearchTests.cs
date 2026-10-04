using System.Net;
using API.Tmdb;
using Domain.Media;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.TMDB;

public class TmdbSearchTests
{
	private const string Results = """
		{"page":1,"results":[
		 {"adult":false,"backdrop_path":"/b.jpg","id":687163,"title":"Project Hail Mary","original_title":"Project Hail Mary","overview":"A teacher wakes up alone.","poster_path":"/p.jpg","media_type":"movie","original_language":"en","genre_ids":[878],"popularity":90.1,"release_date":"2026-03-20","video":false,"vote_average":8.1,"vote_count":2310},
		 {"adult":false,"id":1234,"name":"Mary Shelley","media_type":"person","popularity":3.2},
		 {"adult":false,"backdrop_path":null,"id":95557,"name":"Hail Mary","original_name":"Hail Mary","overview":"","poster_path":null,"media_type":"tv","original_language":"en","genre_ids":[],"popularity":4.4,"first_air_date":"2023-01-05","vote_average":0,"vote_count":0}
		],"total_pages":1,"total_results":3}
		""";

	private readonly MockHttpHandler _tmdb = Mock.HttpHandler();

	[Test]
	public async Task SearchAsync_ForMoviesAndShows_ReturnsBothByTheirTmdbIdAndSkipsPeople()
	{
		_tmdb.OnAnyRequest().RespondWithJson(Results);

		var found = await Testee.SearchAsync("project hail", [MediaType.Movie, MediaType.Show]);

		IExternalMediaId[] expected = [TmdbId.From("687163", MediaType.Movie), TmdbId.From("95557", MediaType.Show)];
		await Assert.That(found.Select(item => item.Externals.Single())).IsEquivalentTo(expected);
		await Assert.That(found[0].Details.Title).IsEqualTo("Project Hail Mary");
		await Assert.That(found[0].Details.ReleaseDate).IsEqualTo(new DateOnly(2026, 3, 20));
		_tmdb.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*/search/multi\\?query=project\\+hail&include_adult=false.*"), Times.Once);
	}

	[Test]
	public async Task SearchAsync_ForMoviesOnly_LeavesTheShowsOut()
	{
		_tmdb.OnAnyRequest().RespondWithJson(Results);

		var found = await Testee.SearchAsync("project hail", [MediaType.Movie]);

		string?[] expected = ["Project Hail Mary"];
		await Assert.That(found.Select(item => item.Details.Title)).IsEquivalentTo(expected);
	}

	[Test]
	public async Task SearchAsync_WhenTmdbIsRateLimited_Fails()
	{
		_tmdb.OnAnyRequest().Respond(HttpStatusCode.TooManyRequests);

		await Assert.That(async () => await Testee.SearchAsync("project hail", [MediaType.Movie])).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task LookUpAsync_ForATvdbId_FindsTheTmdbTitleAndReturnsItsDetailsWithEveryId()
	{
		_tmdb
			.OnRequest(request => request.PathMatches(".*/find/81189\\?external_source=tvdb_id&.*"))
			.RespondWithJson("""{"movie_results":[],"tv_results":[{"id":1396,"name":"Breaking Bad"}]}""");
		_tmdb
			.OnRequest(request => request.PathMatches(".*/tv/1396\\?append_to_response=external_ids&.*"))
			.RespondWithJson(
				"""{"id":1396,"name":"Breaking Bad","media_type":"tv","first_air_date":"2008-01-20","number_of_seasons":5,"external_ids":{"imdb_id":"tt0903747","tvdb_id":81189}}"""
			);

		var found = await Testee.LookUpAsync(TvdbId.From("81189", MediaType.Show));

		await Assert
			.That(found)
			.IsSuccess()
			.And.Multiple(async show =>
			{
				await Assert.That(show.Details.Title).IsEqualTo("Breaking Bad");
				IExternalMediaId[] expected = [TmdbId.From("1396", MediaType.Show), ImdbId.From("tt0903747", MediaType.Show), TvdbId.From("81189", MediaType.Show)];
				await Assert.That(show.Externals).IsEquivalentTo(expected);
			});
	}

	private TmdbSearch Testee => new(TmdbApis.Over(_tmdb), new TmdbArtwork(TmdbApis.Options));
}
