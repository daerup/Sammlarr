using API.Plex;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TMDB;

internal class TmdbIdConverter(PlexApi plexApi)
{
	public async Task<ResolvedId> ConvertAsync(IMediaId mediaId)
	{
		return mediaId switch
		{
			PlexId plexId => await plexApi.ResolveById<TmdbId>(plexId),
			TmdbId tmdbId => new ResolvedId(tmdbId, []),
			ImdbId imdbId => throw new NotSupportedException(),
			TvdbId tvdbId => throw new NotSupportedException(),
		};
	}
}
