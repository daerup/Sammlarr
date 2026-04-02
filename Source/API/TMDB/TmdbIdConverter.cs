using API.Plex;
using API.Plex.Types;
using Contracts.Media;
using Shared;

namespace API.TMDB;

public sealed class TmdbIdConverter(TmdbApi tmdbApi, PlexApi plexApi, IMediaIdStore idStore) : IMediaIdConverter
{
	public async Task<List<IMediaId>> ConvertAsync(IMediaId mediaId, MediaType mediaType)
	{
		if (mediaId is not TmdbId tmdbId)
		{
			return [];
		}

		var cachedIds = await idStore.GetByIdAsync(tmdbId);
		if (cachedIds.OfType<PlexId>().Any())
		{
			return cachedIds;
		}
		var details = await tmdbApi.GetDetailAsync(tmdbId, mediaType);
		var searchResult = await plexApi.SearchAsync(details.DisplayOriginalTitle, mediaType);
		var match = await searchResult.ToAsyncEnumerable().SelectAwait(LoadPlexDetailsAsync).FirstOrDefaultAsync(d => d.TmdbId.Contains(tmdbId));
		if (match is null)
		{
			return [];
		}

		List<IMediaId> allIds = [match.PlexId, .. match.TmdbId, .. match.ImdbId, .. match.TvdbId];

		if (cachedIds.Any())
		{
			await idStore.AppendAsync(cachedIds.First(), allIds);
		}
		else
		{
			await idStore.CreateAsync(allIds);
		}
		var tmdbIds = allIds.OfType<TmdbId>().Cast<IMediaId>();
		return allIds.Except(tmdbIds).ToList();
	}

	private async ValueTask<PlexTypes.ById.Metadata> LoadPlexDetailsAsync(PlexTypes.Search.Metadata metadata)
	{
		return await plexApi.ByIdAsync(metadata.PlexId);
	}
}
