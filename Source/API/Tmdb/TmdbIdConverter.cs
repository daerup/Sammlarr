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
		var tmdbIds = plexResult.AdditionalIds.OfType<TmdbId>().Cast<IMediaId>().ToList();
		var otherIds = plexResult.AdditionalIds.Except(tmdbIds).Append(plexResult.SourceId);
		if (tmdbIds.Any())
		{
			var tmdbId = tmdbIds.First();
			var remainingIds = tmdbIds.Skip(1);
			return new ResolvedId(tmdbId, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}
}
