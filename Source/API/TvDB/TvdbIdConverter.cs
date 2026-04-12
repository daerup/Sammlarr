using API.Plex;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TvDB;

internal class TvdbIdConverter(PlexApi plexApi)
{
	public async Task<ResolvedId> ConvertAsync(IMediaId mediaId)
	{
		return mediaId switch
		{
			PlexId plexId => await plexApi.ResolveById<TvdbId>(plexId),
			TmdbId tmdbId => throw new NotSupportedException(),
			ImdbId imdbId => throw new NotSupportedException(),
			TvdbId tvdbId => new ResolvedId(tvdbId, []),
		};
	}
}
