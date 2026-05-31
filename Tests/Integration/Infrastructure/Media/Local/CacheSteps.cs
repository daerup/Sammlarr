using Integration.Tests.Infrastructure.Media;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media.Local;

[Binding]
public class CacheSteps(MediaCatalogContext catalog, LocalMediaContext local, MediaSeeder seeder)
{
	[Given("media in cache:")]
	public async Task GivenInCache(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
			await Apply(row.Media!, row);
	}

	private async Task Apply(string media, MediaRow row)
	{
		var entry = catalog[media];
		local.MediaIds[media] = await seeder.SeedCachedAsync(entry, row);
		local.Ids[media] = MediaVersionResolver.Resolve(entry, row).Ids;
	}
}
