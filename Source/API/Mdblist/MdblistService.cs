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

		var shows = result.shows.Select(FeedItemResolution.Show);
		var movies = result.movies.Select(FeedItemResolution.Movie);

		var pipeline = shows
			.Concat(movies)
			.ToAsyncEnumerable()
			.SelectAwait(
				async pair => await ResolveAndFetchAsync(pair, watchlistProvider, cancellationToken),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var item in pipeline)
		{
			yield return item;
		}
	}

	private async ValueTask<IMediaDetailResult> ResolveAndFetchAsync(
		FeedItemResolution pair,
		MediaProvider watchlistProvider,
		CancellationToken cancellationToken
	)
	{
		var candidates = ExtractIds(pair.ListItem.ids, pair.MediaType);
		using var activity = SammlarrTelemetry.StartFeedItem(pair.ListItem.title, pair.ListItem.release_year, candidates);
		var conversion = await mediaIdResolver.ResolveAsync(candidates, watchlistProvider, BuildHints(pair));
		if (conversion is Resolved resolved)
		{
			return await metadataProvider.GetAsync(resolved.Id, cancellationToken);
		}
		return BuildUnresolved(pair.ListItem, pair.MediaType);
	}

	private static Some<ResolutionHints> BuildHints(FeedItemResolution pair) =>
		Some.Of(
			new ResolutionHints
			{
				Title = pair.ListItem.title,
				Year = pair.ListItem.release_year,
				MediaType = pair.MediaType,
			}
		);

	private UnresolvedMediaResult BuildUnresolved(MdblistTypes.ListItems.IListItem item, MediaType mediaType)
	{
		var ids = ExtractIds(item.ids, mediaType);
		return new UnresolvedMediaResult
		{
			SourceId = ids[0],
			AdditionalIds = ids[1..],
			MediaType = mediaType,
			Title = item.title,
			ReleaseDate = item.release_year > 0 ? new DateOnly(item.release_year, 1, 1) : DateOnly.MinValue,
		};
	}

	private IMediaId[] ExtractIds(MdblistTypes.ListItems.Ids ids, MediaType mediaType)
	{
		MdblistId? mdblistId = !string.IsNullOrEmpty(ids.mdblist) ? MdblistId.From(ids.mdblist, mediaType) : null;
		TmdbId? tmdbId = ids.tmdb.HasValue ? TmdbId.From(ids.tmdb.Value.ToString(), mediaType) : null;
		ImdbId? imdbId = !string.IsNullOrEmpty(ids.imdb) ? ImdbId.From(ids.imdb, mediaType) : null;
		TvdbId? tvdbId = ids.tvdb.HasValue ? TvdbId.From(ids.tvdb.Value.ToString(), mediaType) : null;

		return [.. new IMediaId?[] { mdblistId, tmdbId, imdbId, tvdbId }.OfType<IMediaId>()];
	}

	private readonly record struct FeedItemResolution
	{
		public MdblistTypes.ListItems.IListItem ListItem { get; private init; }
		public MediaType MediaType { get; private init; }

		public static FeedItemResolution Show(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Show };

		public static FeedItemResolution Movie(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Movie };
	}
}
