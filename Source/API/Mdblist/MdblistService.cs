using System.Diagnostics;
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
		var feedContext = activity?.Context ?? default;
		var endpoint = string.Format(ListItemsEndpoint, identifier);
		var response = await client.SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(stream)!;
		var providerConfig = await configRepository.GetAsync(cancellationToken);
		var watchlistProvider = providerConfig.WatchlistProvider.AsMediaProvider();

		var shows = result.shows.Select(FeedItemResolution.Show);
		var movies = result.movies.Select(FeedItemResolution.Movie);

		var pipeline = shows
			.Concat(movies)
			.ToAsyncEnumerable()
			.SelectAwait(
				async pair => pair with { ConversionResult = await ResolveIdsAsync(pair, watchlistProvider) },
				IMediaIdResolver.RecommendedConcurrency,
				cancellationToken
			)
			.SelectAwait(
				async feedItemResolution => await BuildMediaDetail(feedItemResolution, cancellationToken, feedContext),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var item in pipeline)
		{
			yield return item;
		}
	}

	private async ValueTask<IMediaDetailResult> BuildMediaDetail(
		FeedItemResolution feedItemResolution,
		CancellationToken cancellationToken,
		ActivityContext feedContext
	)
	{
		if (feedItemResolution.ConversionResult is Resolved resolved)
		{
			return await metadataProvider.GetAsync(resolved.Id, cancellationToken, feedContext);
		}
		return BuildUnresolved(feedItemResolution.ListItem, feedItemResolution.MediaType);
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

	private Task<ConversionResult> ResolveIdsAsync(FeedItemResolution feedItemResolution, MediaProvider watchlistProvider)
	{
		var (mdblistId, tmdbId, imdbId, tvdbId) = ExtractIds(feedItemResolution.ListItem.ids, feedItemResolution.MediaType);
		var candidates = new IMediaId?[] { mdblistId, tmdbId, imdbId, tvdbId }
			.OfType<IMediaId>()
			.ToList();
		var hints = Some.Of(
			new ResolutionHints
			{
				Title = feedItemResolution.ListItem.title,
				Year = feedItemResolution.ListItem.release_year,
				MediaType = feedItemResolution.MediaType,
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

	private record struct FeedItemResolution
	{
		public MdblistTypes.ListItems.IListItem ListItem { get; init; }
		public MediaType MediaType { get; private init; }
		public ConversionResult ConversionResult { get; init; }

		public static FeedItemResolution Show(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Show };

		public static FeedItemResolution Movie(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Movie };
	}
}
