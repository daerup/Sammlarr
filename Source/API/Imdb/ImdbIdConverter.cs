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
		var imdbIds = plexResult.AdditionalIds.OfType<TmdbId>().Cast<IMediaId>().ToList();
		var otherIds = plexResult.AdditionalIds.Except(imdbIds).Append(plexResult.SourceId);
		if (imdbIds.Any())
		{
			var imdbId = imdbIds.First();
			var remainingIds = imdbIds.Skip(1);
			return new ResolvedId(imdbId, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}
}
