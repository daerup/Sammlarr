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
			PlexId plexId => await ResolveById(plexId),
			TmdbId tmdbId => throw new NotSupportedException(),
			ImdbId imdbId => new ResolvedId(imdbId, []),
			TvdbId tvdbId => throw new NotSupportedException(),
		};
	}

	private async Task<ResolvedId> ResolveById(PlexId plexId)
	{
		var plexResult = await plexApi.ByIdAsync(plexId);
		IMediaId[] otherIds = [plexResult.PlexId, .. plexResult.TmdbIds, .. plexResult.TvdbIds];
		if (plexResult.ImdbIds.Any())
		{
			var tmdbId = plexResult.ImdbIds.First();
			var remainingIds = plexResult.ImdbIds.Skip(1).Cast<IMediaId>();
			return new ResolvedId(tmdbId, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}
}
