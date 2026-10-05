using System.Net;
using API.Tmdb;
using Domain.Media;
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

	private TmdbSearch Testee => new(TmdbApis.Over(_tmdb), new TmdbArtwork(TmdbApis.Options));
}
