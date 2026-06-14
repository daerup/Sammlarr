using Contracts.Media;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Plex;

[Binding]
public class PlexSteps(MediaCatalogContext catalog, SammlarrScenarioContext sammlarr)
{
	[Given("Plex holds:")]
	public void GivenPlexHolds(DataTable table)
	{
		foreach (var row in table.CreateSet<PlexHoldsRow>())
		{
			var entry = catalog.Resolve(row.Key!);
			var resolved = MediaVersionResolver.BuildSubset(
				entry,
				MediaProviders.ParseList(row.Providers),
				Cells.Text(row.Title),
				Cells.Text(row.Summary),
				row.Genres
			);
			PlexStub.Lookup(sammlarr.HttpHandler, resolved);
		}
	}

	[Given("Plex search for {string} returns:")]
	public void GivenPlexSearchReturns(string term, DataTable table) =>
		PlexStub.Search(sammlarr.HttpHandler, term, [.. table.CreateSet<PlexSearchRow>().Select(ToSearchHit)]);

	[Given("Plex search for {string} returns nothing")]
	public void GivenPlexSearchReturnsNothing(string term) => PlexStub.Search(sammlarr.HttpHandler, term, []);

	[Given("Plex lookup fails for {string}")]
	public void GivenPlexLookupFails(string reference) => PlexStub.LookupFails(sammlarr.HttpHandler, MediaVersionResolver.Build(catalog.Resolve(reference)));

	private static ResolvedVersion ToSearchHit(PlexSearchRow row)
	{
		var plexId = row.Id!.Contains(':') ? MediaProviders.ParseToken(row.Id!).Value : row.Id!.Trim();
		var type = row.Type is null ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
		return new ResolvedVersion(
			new Dictionary<MediaProvider, string> { [MediaProvider.Plex] = plexId },
			type,
			MediaIdDefaults.DefaultYear,
			Cells.Text(row.Title) ?? string.Empty,
			null,
			null,
			null
		);
	}
}
