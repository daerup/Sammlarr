using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API;

internal class MediaIdResolver(
	IMediaIdGroupRepository mediaIdGroupRepository,
	TmdbIdConverter tmdb,
	PlexIdConverter plex,
	ImdbIdConverter imdb,
	TvdbIdConverter tvdb
) : IMediaIdResolver
{
	public async Task<ResolvedId> ResolveAsync(IMediaId sourceId, MediaProvider target)
	{
		var cachedIds = await mediaIdGroupRepository.GetByIdAsync(sourceId);
		var cachedIdWithRequestedTarget = cachedIds.FirstOrDefault(id => id.Source == target);
		if (cachedIdWithRequestedTarget is not null)
		{
			return new ResolvedId(cachedIdWithRequestedTarget, cachedIds.Where(id => id.Source != target));
		}
		var resolved = target switch
		{
			MediaProvider.Plex => await plex.ConvertAsync(sourceId),
			MediaProvider.Tmdb => await tmdb.ConvertAsync(sourceId),
			MediaProvider.Imdb => await imdb.ConvertAsync(sourceId),
			MediaProvider.Tvdb => await tvdb.ConvertAsync(sourceId),
		};

		if (resolved.AllIds.Any())
		{
			await mediaIdGroupRepository.CreateOrAppendAsync(resolved.AllIds);
		}

		return resolved;
	}
}
