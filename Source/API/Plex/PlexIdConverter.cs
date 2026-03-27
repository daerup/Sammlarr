using Contracts.Media;
using DB.Data.Stores;

namespace API.Plex;

internal sealed class PlexIdConverter(PlexApi plexApi, MediaIdRepository mediaIdRepository) : IMediaIdConverter
{
	public async Task<List<IMediaId>> ConvertAsync(IMediaId mediaId, MediaType mediaType)
	{
		if (mediaId is not PlexId plexId)
		{
			return [];
		}

		var cachedExternalIds = await mediaIdRepository.GetByIdAsync(plexId);
		if (cachedExternalIds.Count > 0)
		{
			return cachedExternalIds;
		}

		var plexMetaData = await plexApi.ByIdAsync(plexId);
		List<IMediaId> ids = [.. plexMetaData.TmdbId, .. plexMetaData.TvdbId, .. plexMetaData.ImdbId];

		await mediaIdRepository.CreateAsync(ids.Prepend(plexId).ToList());
		return ids;
	}
}
