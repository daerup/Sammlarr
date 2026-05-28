using Contracts.Media;
using DB.Data.Media;

namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class MediaResolutionSteps(SammlarrScenarioContext sammlarr, MediaCatalogContext catalog, CachedMediaContext cachedMedia)
{
	[Given("{string} is cached in the media database with {string}")]
	public async Task GivenCachedWithIds(string key, string ids)
	{
		var media = catalog[key];
		var entity = new MediaEntity();
		foreach (var idType in ids.Split(',', StringSplitOptions.TrimEntries))
		{
			IExternalMediaId externalId = idType switch
			{
				"TmdbId" => TmdbId.From(media.TmdbId.ToString(), media.MediaType),
				"PlexId" => PlexId.From($"plex-{media.TmdbId}", media.MediaType),
				_ => throw new ArgumentOutOfRangeException(nameof(ids), idType, null),
			};
			await sammlarr.DbContext.ExternalMediaIds.AddAsync(externalId.ToExternalIdEntity(entity));
		}
		await sammlarr.DbContext.SaveChangesAsync();
		sammlarr.DbContext.ChangeTracker.Clear();
		cachedMedia.MediaIds[key] = MediaId.From(entity.Id);
	}
}
