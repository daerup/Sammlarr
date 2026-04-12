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
			PlexId plexId => await ResolveById(plexId),
			TmdbId tmdbId => new ResolvedId(tmdbId, []),
			ImdbId imdbId => throw new NotSupportedException(),
			TvdbId tvdbId => throw new NotSupportedException(),
		};
	}

	private async Task<ResolvedId> ResolveById(PlexId plexId)
	{
		var plexResult = await plexApi.ByIdAsync(plexId);
		IMediaId[] otherIds = [plexResult.PlexId, .. plexResult.ImdbIds, .. plexResult.TvdbIds];
		if (plexResult.TmdbIds.Any())
		{
			var tmdbId = plexResult.TmdbIds.First();
			var remainingIds = plexResult.TmdbIds.Skip(1).Cast<IMediaId>();
			return new ResolvedId(tmdbId, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}
}
