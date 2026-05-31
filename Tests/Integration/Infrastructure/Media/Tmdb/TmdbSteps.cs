using API.Tmdb;
using Contracts.Media;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Tmdb;

[Binding]
public class TmdbSteps(MediaCatalogContext catalog, TmdbContext tmdb, SammlarrScenarioContext sammlarr)
{
	[Given("tmdb has no detail for {string}")]
	public void GivenTmdbNoDetail(string media)
	{
		var resolved = MediaVersionResolver.Resolve(catalog[media], new MediaRow());
		var tmdbId = resolved.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		sammlarr.HttpHandler.RespondWithJsonForUrl(string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, tmdbId), """{ "id": 0 }""");
	}

	[Given("tmdb has:")]
	public void GivenInTmdb(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
		{
			var resolved = MediaVersionResolver.Resolve(catalog[row.Media!], row);
			tmdb.Versions[row.Media!] = resolved;
			var voteAverage = Cells.Number(row.VoteAverage);
			TmdbStub.Detail(sammlarr.HttpHandler, resolved, voteAverage ?? 0, row.VoteCount ?? 0, hasVoteData: voteAverage is not null);
		}
	}

	[Given("TMDB returns no vote data for {string}")]
	public void GivenNoVoteData(string title)
	{
		var resolved = MediaVersionResolver.Resolve(catalog[title], new MediaRow());
		tmdb.Versions[title] = resolved;
		TmdbStub.Detail(sammlarr.HttpHandler, resolved, hasVoteData: false);
	}
}
