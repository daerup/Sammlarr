using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Mdblist.Types;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Settings;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistService(MdblistClient client, IMediaService media, IProviderConfigService providerConfig, IApiKeyService apiKeys) : IFeedProvider
{
	private const int PageSize = 1000;
	private const string HasMoreHeader = "X-Has-More";

	public async IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		var watchlistProvider = config.WatchlistProvider.AsMediaProvider();
		var unknowns = new ConcurrentQueue<MdblistTypes.ListItems.UnknownListItem>();

		var requests = BuildRequestsAsync(identifier, watchlistProvider, unknowns, cancellationToken);
		await foreach (var result in media.ResolveDetailsAsync(requests, cancellationToken))
		{
			yield return result;
		}

		foreach (var unknown in unknowns)
		{
			yield return BuildUnknown(unknown);
		}
	}

	private async IAsyncEnumerable<ResolveRequest> BuildRequestsAsync(
		string identifier,
		MediaProvider watchlistProvider,
		ConcurrentQueue<MdblistTypes.ListItems.UnknownListItem> unknowns,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		await foreach (var page in FetchPagesAsync(identifier, cancellationToken))
		{
			foreach (var unknown in page.unknowns)
			{
				unknowns.Enqueue(unknown);
			}

			var pairs = page.shows.Select(FeedItemResolution.Show).Concat(page.movies.Select(FeedItemResolution.Movie));
			foreach (var pair in pairs)
			{
				yield return new ResolveRequest(ExtractIds(pair.ListItem.ids, pair.MediaType), watchlistProvider, BuildHints(pair));
			}
		}
	}

	private async IAsyncEnumerable<MdblistTypes.ListItems.Root> FetchPagesAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var offset = 0;
		var hasMore = true;
		while (hasMore)
		{
			(var page, hasMore) = await FetchPageAsync(identifier, offset, cancellationToken);
			yield return page;
			offset += PageSize;
		}
	}

	private async Task<(MdblistTypes.ListItems.Root Page, bool HasMore)> FetchPageAsync(string identifier, int offset, CancellationToken cancellationToken)
	{
		var endpoint = string.Format(MdblistEndpoints.ListItems, identifier, PageSize, offset);
		var apiKey = await apiKeys
			.GetApiKeyAsync(ApiKeyProvider.Mdblist, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No MDBList API key is configured. Add one in Settings."));
		using var response = await client.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			throw new FeedIdentifierNotFoundException(identifier);
		}
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var page = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(stream)!;
		var hasMore = response.Headers.TryGetValues(HasMoreHeader, out var values) && values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
		return (page, hasMore);
	}

	private static Some<ResolutionHints> BuildHints(FeedItemResolution pair)
	{
		return Some.Of(
			new ResolutionHints
			{
				Title = pair.ListItem.title,
				Year = pair.ListItem.release_year,
				MediaType = pair.MediaType,
			}
		);
	}

	private static MediaLookup BuildUnknown(MdblistTypes.ListItems.UnknownListItem item) =>
		new UnmatchedMedia
		{
			Externals = [.. ExtractIds(item.ids, item.MediaType)],
			Title = item.title,
			ReleaseDate = item.release_year > 0 ? new DateOnly(item.release_year, 1, 1) : null,
		};

	private static IExternalMediaId[] ExtractIds(MdblistTypes.ListItems.Ids ids, MediaType mediaType)
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
