using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API;

internal class MediaIdResolver(IMediaIdGroupStore mediaIdGroupStore, TmdbIdConverter tmdb, PlexIdConverter plex, ImdbIdConverter imdb, TvdbIdConverter tvdb)
	: IMediaIdResolver
{
	public async Task<ResolvedId> ResolveAsync<TMediaId>(TMediaId sourceId, MediaProvider target)
		where TMediaId : struct, IMediaId
	{
		var cachedIds = await mediaIdGroupStore.GetByIdAsync(sourceId);
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
			await mediaIdGroupStore.CreateOrAppendAsync(resolved.AllIds);
		}

		return resolved;
	}
}
