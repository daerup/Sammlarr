using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace API.Plex;

internal class PlexWatchListService(PlexClient plexClient, [FromKeyedServices(MediaProvider.Plex)] IMediaIdConverter plexIdConverter) : IWatchlistService
{
	private const string AddToWatchlistEndpoint = "https://vod.provider.plex.tv/actions/addToWatchlist?ratingKey={0}";
	private const string RemoveToWatchlistEndpoint = "https://vod.provider.plex.tv/actions/removeFromWatchlist?ratingKey={0}";
	private const string WatchlistEndpoint = "https://discover.provider.plex.tv/library/sections/watchlist/all";

	public async Task<WatchlistPageResult> GetWatchlistAsync(int page, CancellationToken cancellationToken = default)
	{
		const string containerStartHeader = "X-Plex-Container-Start";
		var offset = (page - 1) * PlexClient.ContainerSize;
		var response = await plexClient.SendAsync(HttpMethod.Get, WatchlistEndpoint, cancellationToken, null, (containerStartHeader, offset.ToString()));
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result =
			await JsonSerializer.DeserializeAsync<PlexTypes.Watchlist.Root>(stream, cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Plex watchlist response was empty.");

		var items = await result
			.MediaContainer.Metadata.Where(m => !IsIncomplete(m))
			.ToAsyncEnumerable()
			.SelectAwait(BuildWatchlistItemAsync)
			.ToListAsync(cancellationToken);

		var totalPages = result.MediaContainer.TotalSize <= 0 ? 1 : (int)Math.Ceiling(result.MediaContainer.TotalSize / (double)PlexClient.ContainerSize);

		return new WatchlistPageResult(items, totalPages);
	}

	private static bool IsIncomplete(PlexTypes.Watchlist.Metadata arg)
	{
		return arg.Year == 0 || string.IsNullOrEmpty(arg.OriginallyAvailableAt) || string.IsNullOrEmpty(arg.Title);
	}

	public async Task<bool> AddToWatchlistAsync(PlexId ratingKey, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(AddToWatchlistEndpoint, Uri.EscapeDataString(ratingKey.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Put, endpoint, cancellationToken, new StringContent(string.Empty));
		return response.IsSuccessStatusCode;
	}

	public async Task<bool> RemoveFromWatchlistAsync(PlexId ratingKey, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(RemoveToWatchlistEndpoint, Uri.EscapeDataString(ratingKey.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Put, endpoint, cancellationToken, new StringContent(string.Empty));
		return response.IsSuccessStatusCode;
	}

	private async ValueTask<PlexWatchlistItem> BuildWatchlistItemAsync(PlexTypes.Watchlist.Metadata parsed)
	{
		var externalIds = await plexIdConverter.ConvertAsync(parsed.PlexId, MediaType.Unknown);
		return new PlexWatchlistItem
		{
			Title = parsed.Title,
			MediaType = MediaType.FromString(parsed.Type),
			Description = parsed.Tagline ?? string.Empty,
			ArtUrl = parsed.Art,
			PosterUrl = parsed.Thumb,
			AddedDate = DateTimeOffset.FromUnixTimeSeconds(parsed.AddedAt),
			ReleaseDate = DateTimeOffset.Parse(parsed.OriginallyAvailableAt),
			RatingKey = parsed.PlexId,
			TmdbId = externalIds.OfType<TmdbId>().ToArray(),
			TvdbId = externalIds.OfType<TvdbId>().ToArray(),
			ImdbId = externalIds.OfType<ImdbId>().ToArray(),
		};
	}
}
