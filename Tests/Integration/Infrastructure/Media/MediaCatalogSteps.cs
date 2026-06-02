using System;
using Contracts.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class MediaCatalogSteps(MediaCatalogContext catalog, MediaSeeder seeder, LocalMediaContext local)
{
	[Given("the following media:")]
	[Given("additional media:")]
	public async Task GivenMedia(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
		{
			var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
			var ids = row.Ids ?? throw new InvalidOperationException($"Media '{row.Media}' must declare an 'ids' column.");
			var entry = catalog.Register(row.Media!, type, ids);
			await SeedStore(entry, row);
		}
	}

	private async Task SeedStore(CatalogEntry entry, MediaRow row)
	{
		var store = row.Store?.Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(store))
			return;

		local.MediaIds[entry.Key] = store switch
		{
			"db" => await seeder.SeedIdentityAsync(entry, row),
			"cache" => await seeder.SeedCachedAsync(entry, row),
			_ => throw new InvalidOperationException($"Unknown store '{row.Store}' for media '{entry.Key}'. Use 'db' or 'cache'."),
		};
		local.Ids[entry.Key] = MediaVersionResolver.Resolve(entry, row).Ids;
	}
}
