using Contracts.Media;
using Integration.Tests.Infrastructure.Media;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Local;

[Binding]
public class CacheSteps(MediaCatalogContext catalog, LocalMediaContext local, MediaSeeder seeder)
{
	[Given("the library has cached:")]
	public async Task GivenLibraryHasCached(DataTable table)
	{
		foreach (var row in table.CreateSet<CachedMediaRow>())
		{
			CatalogEntry entry;
			ResolvedVersion resolved;
			if (row.Providers is not null)
			{
				entry = catalog.Resolve(row.Key!);
				resolved = MediaVersionResolver.BuildSubset(
					entry,
					MediaProviders.ParseList(row.Providers),
					Cells.Text(row.Title),
					Cells.Text(row.Summary),
					row.Genres,
					Cells.Text(row.Poster)
				);
			}
			else
			{
				var ids =
					row.Ids ?? throw new InvalidOperationException("A 'the library has cached:' row must declare 'ids' or reference a declared media via 'with ids of'.");
				var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
				entry = catalog.Register(ids, type, row.Key, year: null);
				resolved = MediaVersionResolver.Build(entry, Cells.Text(row.Title), Cells.Text(row.Summary), row.Genres, Cells.Text(row.Poster));
			}
			local.Record(entry, await seeder.SeedCachedAsync(resolved, Cells.Date(row.Fetched)));
		}
	}
}
