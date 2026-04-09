using Contracts.Media;

namespace API.Plex;

internal class PlexIdConverter(PlexApi plexApi, IMediaIdGroupStore mediaIdGroupStore) : IMediaIdConverter
{
	public async Task<List<IMediaId>> ConvertAsync(IMediaId mediaId)
	{
		if (mediaId is not PlexId plexId)
		{
			return [];
		}

		var cachedExternalIds = await mediaIdGroupStore.GetByIdAsync(plexId);
		if (cachedExternalIds.Count > 0)
		{
			return cachedExternalIds;
		}

		var plexMetaData = await plexApi.ByIdAsync(plexId);
		List<IMediaId> ids = [.. plexMetaData.TmdbId, .. plexMetaData.TvdbId, .. plexMetaData.ImdbId];

		await mediaIdGroupStore.CreateOrAppendAsync(ids.Prepend(plexId));
		return ids;
	}
}
