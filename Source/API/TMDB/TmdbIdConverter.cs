using API.Plex;
using API.Plex.Types;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Shared;

namespace API.TMDB;

internal class TmdbIdConverter(
	TmdbApi tmdbApi,
	PlexApi plexApi,
	IMediaIdMappingStore idMappingStore,
	IKeyedServiceIndex<MediaProvider, IMediaSearchService> searchServiceIndex
) : IMediaIdConverter
{
	public async Task<List<IMediaId>> ConvertAsync(IMediaId mediaId, MediaType mediaType)
	{
		if (mediaId is not TmdbId tmdbId)
		{
			return [];
		}

		var cachedIds = await idMappingStore.GetByIdAsync(tmdbId);
		if (cachedIds.OfType<PlexId>().Any())
		{
			return cachedIds;
		}
		var details = await tmdbApi.GetDetailAsync(tmdbId, mediaType);
		var searchResults = await searchServiceIndex[MediaProvider.Plex].SearchAsync(details.DisplayOriginalTitle, [mediaType]);
		var match = await searchResults
			.Select(s => s.SourceId)
			.OfType<PlexId>()
			.ToAsyncEnumerable()
			.SelectAwait(LoadPlexDetailsAsync)
			.FirstOrDefaultAsync(d => d.TmdbId.Contains(tmdbId));
		if (match is null)
		{
			return [];
		}

		List<IMediaId> allIds = [match.PlexId, .. match.TmdbId, .. match.ImdbId, .. match.TvdbId];
		if (cachedIds.Any())
		{
			await idMappingStore.AppendAsync(cachedIds.First(), allIds);
		}
		else
		{
			await idMappingStore.CreateAsync(allIds);
		}
		var tmdbIds = allIds.OfType<TmdbId>().Cast<IMediaId>();
		return allIds.Except(tmdbIds).ToList();
	}

	private async ValueTask<PlexTypes.ById.Metadata> LoadPlexDetailsAsync(PlexId plexId)
	{
		return await plexApi.ByIdAsync(plexId);
	}
}
