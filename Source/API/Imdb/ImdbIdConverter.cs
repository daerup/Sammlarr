using API.Plex;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.IMDB;

internal class ImdbIdConverter(PlexApi plexApi)
{
	public async Task<ResolvedId> ConvertAsync(IMediaId mediaId)
	{
		return mediaId switch
		{
			PlexId plexId => await plexApi.ResolveById<ImdbId>(plexId),
			TmdbId tmdbId => throw new NotSupportedException(),
			ImdbId imdbId => new ResolvedId(imdbId, []),
			TvdbId tvdbId => throw new NotSupportedException(),
		};
	}
}
