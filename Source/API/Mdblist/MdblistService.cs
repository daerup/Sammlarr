using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Mdblist.Types;
using Contracts;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;
using Shared.Telemetry;

namespace API.Mdblist;

internal class MdblistService(MdblistClient client, IMediaService media, IProviderConfigRepository configRepository) : IFeedService
{
    private const string ListItemsEndpoint = "lists/{0}/items?limit={1}&offset={2}";
    private const int PageSize = 1000;
    private const string HasMoreHeader = "X-Has-More";

    public async IAsyncEnumerable<IMediaDetail> GetFeedItemsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = SammlarrTelemetry.StartFeedLoad(FeedProvider.Mdblist, identifier);
        var providerConfig = await configRepository.GetAsync(cancellationToken);
        var watchlistProvider = providerConfig.WatchlistProvider.AsMediaProvider();

        await foreach (var page in FetchPagesAsync(identifier, cancellationToken))
        {
            var pairs = page.shows.Select(FeedItemResolution.Show).Concat(page.movies.Select(FeedItemResolution.Movie)).ToList();
            var requests = pairs.Select(pair => new ResolveRequest(ExtractIds(pair.ListItem.ids, pair.MediaType), watchlistProvider, BuildHints(pair)));
            var pipeline = media.GetDetailsAsync(requests, cancellationToken).Zip(pairs, cancellationToken);

            await foreach (var (result, pair) in pipeline)
            {
                yield return result.Value as IMediaDetail ?? BuildUnresolved(pair.ListItem, pair.MediaType);
            }

            foreach (var unknown in page.unknowns)
            {
                yield return BuildUnknown(unknown);
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

    private static Some<ResolutionHints> BuildHints(FeedItemResolution pair)
    {
        return Some.Of(
            new ResolutionHints {
                Title = pair.ListItem.title,
                Year = pair.ListItem.release_year,
                MediaType = pair.MediaType,
            }
        );
    }

    private UnresolvedMedia BuildUnresolved(MdblistTypes.ListItems.IListItem item, MediaType mediaType)
    {
        var ids = ExtractIds(item.ids, mediaType);
        return new UnresolvedMedia
        {
            Identity = new MediaIdentity(default, ids),
            MediaType = mediaType, Title = item.title,
            ReleaseDate = item.release_year > 0 ? new DateOnly(item.release_year, 1, 1) : DateOnly.MinValue,
        };
    }

    private UnresolvedMedia BuildUnknown(MdblistTypes.ListItems.UnknownListItem item)
    {
        var ids = ExtractIds(item.ids, item.MediaType);
        return new UnresolvedMedia
        {
            Identity = new MediaIdentity(default, ids),
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
