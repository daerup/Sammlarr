using Contracts.Media;
using Domain.Media;
using Integration.Tests.Infrastructure.Media;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Local;

[Binding]
public class DbSteps(MediaCatalogContext catalog, LocalMediaContext local, MediaSeeder seeder)
{
	[Given("the library knows:")]
	public async Task GivenLibraryKnows(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaIntroRow>())
		{
			CatalogEntry entry;
			ResolvedVersion resolved;
			if (row.Providers is not null)
			{
				entry = catalog.Resolve(row.Key!);
				resolved = MediaVersionResolver.BuildSubset(entry, MediaProviders.ParseList(row.Providers));
			}
			else
			{
				var ids =
					row.Ids ?? throw new InvalidOperationException("A 'the library knows:' row must declare 'ids' or reference a declared media via 'with ids of'.");
				var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
				entry = catalog.Register(ids, type, row.Key, row.Year);
				resolved = MediaVersionResolver.Build(entry);
			}
			local.Record(entry, await seeder.SeedIdentityAsync(resolved));
		}
	}
}
