using API.Tmdb;
using Contracts.Media;
using Domain.Providers;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Tmdb;

[Binding]
public class TmdbSteps(MediaCatalogContext catalog, TmdbContext tmdb, SammlarrScenarioContext sammlarr)
{
	[Given("TMDB has no detail for {string}")]
	public void GivenTmdbNoDetail(string reference)
	{
		var resolved = MediaVersionResolver.Build(catalog.Resolve(reference));
		var tmdbId = resolved.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		sammlarr.HttpHandler.RespondWithJsonForUrl(string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, tmdbId), """{ "id": 0 }""");
	}

	[Given("TMDB holds:")]
	public void GivenTmdbHolds(DataTable table)
	{
		foreach (var row in table.CreateSet<TmdbHoldsRow>())
		{
			var entry = catalog.Resolve(row.Key!);
			var resolved = MediaVersionResolver.BuildSubset(
				entry,
				MediaProviders.ParseList(row.Providers),
				Cells.Text(row.Title),
				Cells.Text(row.Summary),
				row.Genres
			);
			tmdb.Versions[entry.Alias ?? row.Key!] = resolved;
			var voteAverage = Cells.Number(row.VoteAverage);
			TmdbStub.Detail(sammlarr.HttpHandler, resolved, voteAverage ?? 0, row.VoteCount ?? 0, hasVoteData: voteAverage is not null);
		}
	}
}
