using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Mdblist.Types;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Feed;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;
using Shared.Telemetry;

namespace API.Mdblist;

internal class MdblistService(
	MdblistClient client,
	IMediaIdResolver mediaIdResolver,
	IMediaMetadataProvider metadataProvider,
	IProviderConfigRepository configRepository
) : IFeedService
{
	private const string ListItemsEndpoint = "lists/{0}/items";

	public async IAsyncEnumerable<IMediaDetailResult> GetFeedItemsAsync(
		string identifier,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		using var activity = SammlarrTelemetry.StartFeedLoad(FeedProvider.Mdblist, identifier);
		var endpoint = string.Format(ListItemsEndpoint, identifier);
		var response = await client.SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(stream)!;
		var providerConfig = await configRepository.GetAsync(cancellationToken);
		var watchlistProvider = providerConfig.WatchlistProvider.AsMediaProvider();

		var resolvedIds = new List<IMediaId>();
		var unresolved = new List<IMediaDetailResult>();

		var shows = result.shows.Select(s => ((MdblistTypes.ListItems.IListItem)s, MediaType.Show));
		var movies = result.movies.Select(m => ((MdblistTypes.ListItems.IListItem)m, MediaType.Movie));

		foreach (var (item, mediaType) in shows.Concat(movies))
		{
			if (await ResolveIdsAsync(item.ids, mediaType, watchlistProvider, item.title, item.release_year) is Resolved resolved)
			{
				resolvedIds.Add(resolved.Id);
			}
			else
			{
				unresolved.Add(BuildUnresolved(item, mediaType));
			}
		}

		await foreach (var detail in metadataProvider.GetAsync(resolvedIds, cancellationToken))
		{
			yield return detail;
		}

		foreach (var item in unresolved)
		{
			yield return item;
		}
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
