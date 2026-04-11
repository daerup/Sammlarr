using API.TMDB;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi, [FromKeyedServices(MediaSearchProvider.Plex)] IMediaSearchService searchService)
{
	public async Task<ResolvedId> ConvertAsync(IMediaId mediaId)
	{
		return mediaId switch
		{
			PlexId plexId => new ResolvedId(plexId, await GetAdditionalIds(plexId)),
			TmdbId tmdbId => await FuzzySearch(tmdbId),
			ImdbId imdbId => throw new NotSupportedException(),
			TvdbId tvdbId => throw new NotSupportedException(),
		};
	}

	private async Task<IEnumerable<IMediaId>> GetAdditionalIds(PlexId plexId)
	{
		var details = await plexApi.ByIdAsync(plexId);
		return [.. details.TmdbIds, .. details.TvdbIds, .. details.ImdbIds];
	}

	private async ValueTask<ResolvedId> FuzzySearch(TmdbId tmdbId)
	{
		var details = await tmdbApi.GetDetailAsync(tmdbId);
		var searchResults = await searchService.SearchAsync(details.DisplayOriginalTitle, [tmdbId.Type]);
		var match = await searchResults
			.Select(s => s.SourceId)
			.OfType<PlexId>()
			.ToAsyncEnumerable()
			.SelectAwait(async id => await plexApi.ByIdAsync(id))
			.FirstOrDefaultAsync(d => d.TmdbIds.Contains(tmdbId));

		if (match is null)
		{
			return ResolvedId.Failed;
		}
		List<IMediaId> otherIds = [.. match.TmdbIds, .. match.ImdbIds, .. match.TvdbIds];
		return new ResolvedId(match.PlexId, otherIds);
	}
}
