using Contracts.Media;

namespace API.Plex;

internal class PlexIdConverter(PlexApi plexApi, IMediaIdStore mediaIdStore) : IMediaIdConverter
{
	public async Task<List<IMediaId>> ConvertAsync(IMediaId mediaId, MediaType _)
	{
		if (mediaId is not PlexId plexId)
		{
			return [];
		}

		var cachedExternalIds = await mediaIdStore.GetByIdAsync(plexId);
		if (cachedExternalIds.Count > 0)
		{
			return cachedExternalIds;
		}

		var plexMetaData = await plexApi.ByIdAsync(plexId);
		List<IMediaId> ids = [.. plexMetaData.TmdbId, .. plexMetaData.TvdbId, .. plexMetaData.ImdbId];

		await mediaIdStore.CreateAsync(ids.Prepend(plexId).ToList());
		return ids;
	}
}
