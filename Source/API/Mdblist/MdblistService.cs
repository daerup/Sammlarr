using System.Text.Json;
using API.Mdblist.Types;
using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.List;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;

namespace API.Mdblist;

internal class MdblistService(PlexApi plexApi, MdblistClient client, IMediaIdResolver mediaIdResolver, IProviderConfigRepository configRepository)
	: IImportListService
{
	private const string ListItemsEndpoint = "lists/{0}/items";

	public async Task<IReadOnlyList<IMediaDetailResult>> GetEnrichedImportListItemsAsync(string listIdentifier, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(ListItemsEndpoint, listIdentifier);
		var response = await client.SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(stream)!;
		var providerConfig = await configRepository.GetAsync(cancellationToken);

		var movies = await result
			.movies.ToAsyncEnumerable()
			.SelectAwait(async m => await MapMediaItemAsync(m, MediaType.Movie, providerConfig))
			.ToListAsync(cancellationToken);
		var shows = await result
			.shows.ToAsyncEnumerable()
			.SelectAwait(async s => await MapMediaItemAsync(s, MediaType.Show, providerConfig))
			.ToListAsync(cancellationToken);
		return [.. shows, .. movies];
	}

	private async Task<IMediaDetailResult> MapMediaItemAsync(MdblistTypes.ListItems.IListItem item, MediaType mediaType, ProviderConfig providerConfig)
	{
		var resolvedResult = await ResolveIdsAsync(item.ids, mediaType, providerConfig.WatchlistProvider.AsMediaProvider(), item.title, item.release_year);
		if (resolvedResult is not Resolved resolvedId)
		{
			throw new InvalidOperationException($"Tried to map a {mediaType} from MDBList, but no watchlist provider ID could be resolved");
		}
		return await plexApi.LookUpAsync(resolvedId.Id);
	}

	private Task<ConversionResult> ResolveIdsAsync(
		MdblistTypes.ListItems.Ids mdbdlistIds,
		MediaType mediaType,
		MediaProvider watchlistProvider,
		string title,
		int year
	)
	{
		var (mdblistId, tmdbId, imdbId, tvdbId) = ExtractIds(mdbdlistIds, mediaType);
		var candidates = new IMediaId?[] { mdblistId, tmdbId, imdbId, tvdbId }
			.OfType<IMediaId>()
			.ToList();
		var hints = Some.Of(
			new ResolutionHints
			{
				Title = title,
				Year = year,
				MediaType = mediaType,
			}
		);
		return mediaIdResolver.ResolveAsync(candidates, watchlistProvider, hints);
	}

	private (MdblistId?, TmdbId?, ImdbId?, TvdbId?) ExtractIds(MdblistTypes.ListItems.Ids ids, MediaType mediaType)
	{
		MdblistId? mdblistId = !string.IsNullOrEmpty(ids.mdblist) ? MdblistId.From(ids.mdblist, mediaType) : null;
		TmdbId? tmdbId = ids.tmdb != 0 ? TmdbId.From(ids.tmdb.ToString(), mediaType) : null;
		ImdbId? imdbId = !string.IsNullOrEmpty(ids.imdb) ? ImdbId.From(ids.imdb, mediaType) : null;
		TvdbId? tvdbId = ids.tvdb != 0 ? TvdbId.From(ids.tvdb.ToString(), mediaType) : null;
		return (mdblistId, tmdbId, imdbId, tvdbId);
	}

	public Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();
}
