using System.Net;
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
	private const string ListItemsEndpoint = "lists/{0}/items?limit={1}&offset={2}";
	private const int PageSize = 1000;
	private const string HasMoreHeader = "X-Has-More";

	public async IAsyncEnumerable<IMediaDetailResult> GetFeedItemsAsync(
		string identifier,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		using var activity = SammlarrTelemetry.StartFeedLoad(FeedProvider.Mdblist, identifier);
		var providerConfig = await configRepository.GetAsync(cancellationToken);
		var watchlistProvider = providerConfig.WatchlistProvider.AsMediaProvider();

		await foreach (var page in FetchPagesAsync(identifier, cancellationToken))
		{
			var shows = page.shows.Select(FeedItemResolution.Show);
			var movies = page.movies.Select(FeedItemResolution.Movie);

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

			foreach (var unknown in page.unknowns)
			{
				yield return BuildUnresolved(unknown);
			}
		}
	}

	private async IAsyncEnumerable<MdblistTypes.ListItems.Root> FetchPagesAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var offset = 0;
		while (true)
		{
			var (page, hasMore) = await FetchPageAsync(identifier, offset, cancellationToken);
			yield return page;
			if (!hasMore)
			{
				yield break;
			}
			offset += PageSize;
		}
	}

	private async Task<(MdblistTypes.ListItems.Root Page, bool HasMore)> FetchPageAsync(string identifier, int offset, CancellationToken cancellationToken)
	{
		var endpoint = string.Format(ListItemsEndpoint, identifier, PageSize, offset);
		using var response = await client.SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			throw new FeedIdentifierNotFoundException(identifier);
		}
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var page = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(stream)!;
		var hasMore =
			response.Headers.TryGetValues(HasMoreHeader, out var values) && values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
		return (page, hasMore);
	}

	private async ValueTask<IMediaDetailResult> ResolveAndFetchAsync(
		FeedItemResolution pair,
		MediaProvider watchlistProvider,
		CancellationToken cancellationToken
	)
	{
		var candidates = ExtractIds(pair.ListItem.ids, pair.MediaType);
		using var activity = SammlarrTelemetry.StartFeedItem(pair.ListItem.title, pair.ListItem.release_year, candidates);
		var conversion = await mediaIdResolver.ResolveAsync(candidates, watchlistProvider, BuildHints(pair), cancellationToken);
		if (conversion.Value is Resolved resolved)
		{
			try
			{
				var detail = await metadataProvider.GetAsync(resolved.Id, cancellationToken);
				return detail ?? BuildUnresolved(pair.ListItem, pair.MediaType);
			}
			catch (Exception)
			{
				return BuildUnresolved(pair.ListItem, pair.MediaType);
			}
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
			Id = default,
			Externals = ids,
			MediaType = mediaType,
			Title = item.title,
			ReleaseDate = item.release_year > 0 ? new DateOnly(item.release_year, 1, 1) : DateOnly.MinValue,
		};
	}

	private UnresolvedMediaResult BuildUnresolved(MdblistTypes.ListItems.UnknownListItem item)
	{
		var ids = ExtractIds(item.ids, item.MediaType);
		return new UnresolvedMediaResult
		{
			Id = default,
			Externals = ids,
			MediaType = item.MediaType,
			Title = item.title,
			ReleaseDate = item.release_year > 0 ? new DateOnly(item.release_year, 1, 1) : DateOnly.MinValue,
		};
	}

	private IExternalMediaId[] ExtractIds(MdblistTypes.ListItems.Ids ids, MediaType mediaType)
	{
		MdblistId? mdblistId = !string.IsNullOrEmpty(ids.mdblist) ? MdblistId.From(ids.mdblist, mediaType) : null;
		TmdbId? tmdbId = ids.tmdb.HasValue ? TmdbId.From(ids.tmdb.Value.ToString(), mediaType) : null;
		ImdbId? imdbId = !string.IsNullOrEmpty(ids.imdb) ? ImdbId.From(ids.imdb, mediaType) : null;
		TvdbId? tvdbId = ids.tvdb.HasValue ? TvdbId.From(ids.tvdb.Value.ToString(), mediaType) : null;

		return [.. new IExternalMediaId?[] { mdblistId, tmdbId, imdbId, tvdbId }.OfType<IExternalMediaId>()];
	}

	private readonly record struct FeedItemResolution
	{
		public MdblistTypes.ListItems.IListItem ListItem { get; private init; }
		public MediaType MediaType { get; private init; }

		public static FeedItemResolution Show(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Show };

		public static FeedItemResolution Movie(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Movie };
	}
}
