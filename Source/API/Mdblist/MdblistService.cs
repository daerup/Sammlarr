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

		var movies = await result.movies.ToAsyncEnumerable().SelectAwait(async m => await MapMovieAsync(m, providerConfig)).ToListAsync(cancellationToken);
		var shows = await result.shows.ToAsyncEnumerable().SelectAwait(async s => await MapShowAsync(s, providerConfig)).ToListAsync(cancellationToken);
		return [.. shows, .. movies];
	}

	private async Task<IMediaDetailResult> MapMovieAsync(MdblistTypes.ListItems.Movie movie, ProviderConfig providerConfig)
	{
		var resolvedId = await ResolveIdsAsync(movie.ids, MediaType.Movie, providerConfig.WatchlistProvider.AsMediaProvider());
		// TODO abstract the lookup
		if (resolvedId.Id is not PlexId plexId)
		{
			throw new InvalidOperationException("Tried to map a movie from MDBList to Plex, but the resolved ID was not a Plex ID");
		}
		return await plexApi.ByIdAsync(plexId);
	}

	private async Task<IMediaDetailResult> MapShowAsync(MdblistTypes.ListItems.Show movie, ProviderConfig providerConfig)
	{
		var resolvedId = await ResolveIdsAsync(movie.ids, MediaType.Show, providerConfig.WatchlistProvider.AsMediaProvider());
		// TODO abstract the lookup
		if (resolvedId.Id is not PlexId plexId)
		{
			throw new InvalidOperationException("Tried to map a show from MDBList to Plex, but the resolved ID was not a Plex ID");
		}
		return await plexApi.ByIdAsync(plexId);
	}

	private async Task<ResolvedId> ResolveIdsAsync(MdblistTypes.ListItems.Ids mdbdlistIds, MediaType mediaType, MediaProvider watchlistProvider)
	{
		var (tmdbId, imdbId, tvdbId) = ExtractIds(mdbdlistIds, mediaType);
		var resolvedId = await ResolveIdsAsync(tmdbId, watchlistProvider);
		resolvedId ??= await ResolveIdsAsync(imdbId, watchlistProvider, resolvedId);
		resolvedId ??= await ResolveIdsAsync(tvdbId, watchlistProvider, resolvedId);
		return resolvedId!;
	}

	private async Task<ResolvedId?> ResolveIdsAsync(IMediaId? mediaId, MediaProvider watchlistProvider, ResolvedId? alreadyResolved = null)
	{
		if (alreadyResolved is { Successful: true })
		{
			return alreadyResolved;
		}

		return mediaId is not null ? await mediaIdResolver.ResolveAsync(mediaId, watchlistProvider) : null;
	}

	private (TmdbId?, ImdbId?, TvdbId?) ExtractIds(MdblistTypes.ListItems.Ids ids, MediaType mediaType)
	{
		TmdbId? tmdbId = ids.tmdb != 0 ? TmdbId.From(ids.tmdb.ToString(), mediaType) : null;
		ImdbId? imdbId = !string.IsNullOrEmpty(ids.imdb) ? ImdbId.From(ids.imdb, mediaType) : null;
		TvdbId? tvdbId = ids.tvdb != 0 ? TvdbId.From(ids.tvdb.ToString(), mediaType) : null;
		return (tmdbId, imdbId, tvdbId);
	}

	public Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();
}
