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
			PlexId plexId => await ResolveById(plexId),
			TmdbId tmdbId => throw new NotSupportedException(),
			ImdbId imdbId => throw new NotSupportedException(),
			TvdbId tvdbId => new ResolvedId(tvdbId, []),
		};
	}

	private async Task<ResolvedId> ResolveById(PlexId plexId)
	{
		var plexResult = await plexApi.ByIdAsync(plexId);
		var tvdbIds = plexResult.AdditionalIds.OfType<TmdbId>().Cast<IMediaId>().ToList();
		var otherIds = plexResult.AdditionalIds.Except(tvdbIds).Append(plexResult.SourceId);
		if (tvdbIds.Any())
		{
			var tvdbId = tvdbIds.First();
			var remainingIds = tvdbIds.Skip(1);
			return new ResolvedId(tvdbId, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}
}
