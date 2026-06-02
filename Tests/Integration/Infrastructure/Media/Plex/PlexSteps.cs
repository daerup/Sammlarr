using Contracts.Media;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Plex;

[Binding]
public class PlexSteps(MediaCatalogContext catalog, SammlarrScenarioContext sammlarr)
{
	[Given("plex has:")]
	public void GivenPlexHas(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
			PlexStub.Lookup(sammlarr.HttpHandler, Resolve(row));
	}

	[Given("plex search for {string} returns:")]
	public void GivenPlexSearchReturns(string term, DataTable table) =>
		PlexStub.Search(sammlarr.HttpHandler, term, [.. table.CreateSet<PlexSearchRow>().Select(ToSearchHit)]);

	[Given("plex search for {string} returns nothing")]
	public void GivenPlexSearchReturnsNothing(string term) => PlexStub.Search(sammlarr.HttpHandler, term, []);

	[Given("plex lookup fails for {string}")]
	public void GivenPlexLookupFails(string media) => PlexStub.LookupFails(sammlarr.HttpHandler, Resolve(new MediaRow { Media = media }));

	private ResolvedVersion Resolve(MediaRow row)
	{
		if (row.Ids is null)
			return MediaVersionResolver.Resolve(catalog[row.Media!], row);
		var type = row.Type is null ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
		return MediaVersionResolver.Resolve(new CatalogEntry(row.Media!, row.Media!, type, null, row.Ids), row);
	}

	private static ResolvedVersion ToSearchHit(PlexSearchRow row)
	{
		var plexId = row.Id!.Contains(':') ? row.Id!.Split(':', 2)[1].Trim() : row.Id!.Trim();
		var type = row.Type is null ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
		return new ResolvedVersion(new Dictionary<MediaProvider, string> { [MediaProvider.Plex] = plexId }, type, MediaIdDefaults.DefaultYear, row.Media!, null, null, null);
	}
}

public sealed class PlexSearchRow
{
	public string? Media { get; set; }
	public string? Id { get; set; }
	public string? Type { get; set; }
}
