using System.Text.Json;
using API.Mdblist.Types;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.List;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;

namespace API.Mdblist;

internal class MdblistService(
	MdblistClient client,
	IMediaIdResolver mediaIdResolver,
	IMediaMetadataProvider metadataProvider,
	IProviderConfigRepository configRepository
) : IImportListService
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
			.SelectAwait(async m => await MapMediaItemAsync(m, MediaType.Movie, providerConfig, cancellationToken))
			.ToListAsync(cancellationToken);
		var shows = await result
			.shows.ToAsyncEnumerable()
			.SelectAwait(async s => await MapMediaItemAsync(s, MediaType.Show, providerConfig, cancellationToken))
			.ToListAsync(cancellationToken);
		return [.. shows, .. movies];
	}

	public Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	private async Task<IMediaDetailResult> MapMediaItemAsync(
		MdblistTypes.ListItems.IListItem item,
		MediaType mediaType,
		ProviderConfig providerConfig,
		CancellationToken cancellationToken
	)
	{
		var resolvedResult = await ResolveIdsAsync(item.ids, mediaType, providerConfig.WatchlistProvider.AsMediaProvider(), item.title, item.release_year);
		if (resolvedResult is not Resolved resolvedId)
		{
			return BuildUnresolved(item, mediaType);
		}
		return await metadataProvider.GetAsync(resolvedId.Id, cancellationToken);
	}

	private UnresolvedMediaResult BuildUnresolved(MdblistTypes.ListItems.IListItem item, MediaType mediaType)
	{
		var (mdblistId, tmdbId, imdbId, tvdbId) = ExtractIds(item.ids, mediaType);
		var ids = new IMediaId?[] { imdbId, tmdbId, mdblistId, tvdbId }
			.OfType<IMediaId>()
			.ToArray();
		return new UnresolvedMediaResult
		{
			SourceId = ids[0],
			AdditionalIds = ids[1..],
			MediaType = mediaType,
			Title = item.title,
			ReleaseDate = item.release_year > 0 ? new DateOnly(item.release_year, 1, 1) : DateOnly.MinValue,
		};
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
		TmdbId? tmdbId = ids.tmdb.HasValue ? TmdbId.From(ids.tmdb.Value.ToString(), mediaType) : null;
		ImdbId? imdbId = !string.IsNullOrEmpty(ids.imdb) ? ImdbId.From(ids.imdb, mediaType) : null;
		TvdbId? tvdbId = ids.tvdb.HasValue ? TvdbId.From(ids.tvdb.Value.ToString(), mediaType) : null;
		return (mdblistId, tmdbId, imdbId, tvdbId);
	}
}
