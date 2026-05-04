using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Watchlist;

namespace API.Plex;

internal class PlexWatchListService(PlexClient plexClient) : IWatchlistProvider
{
	private const string AddToWatchlistEndpoint = "https://vod.provider.plex.tv/actions/addToWatchlist?ratingKey={0}";
	private const string RemoveToWatchlistEndpoint = "https://vod.provider.plex.tv/actions/removeFromWatchlist?ratingKey={0}";
	private const string WatchlistEndpoint = "https://discover.provider.plex.tv/library/sections/watchlist/all";

	public async Task<WatchlistProviderPage> GetWatchlistAsync(int page, CancellationToken cancellationToken = default)
	{
		const string containerStartHeader = "X-Plex-Container-Start";
		var offset = (page - 1) * PlexClient.ContainerSize;
		var response = await plexClient.SendAsync(HttpMethod.Get, WatchlistEndpoint, cancellationToken, null, (containerStartHeader, offset.ToString()));
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result =
			await JsonSerializer.DeserializeAsync<PlexTypes.Watchlist.Root>(stream, cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Plex watchlist response was empty.");

		var items = result.MediaContainer.Metadata.Where(m => !IsIncomplete(m)).Select(m => m.PlexId).Cast<IMediaId>().ToList();

		var totalPages = result.MediaContainer.TotalSize <= 0 ? 1 : (int)Math.Ceiling(result.MediaContainer.TotalSize / (double)PlexClient.ContainerSize);

		return new WatchlistProviderPage(items, totalPages);
	}

	public async Task<bool> AddToWatchlistAsync(IMediaId ratingKey, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(AddToWatchlistEndpoint, Uri.EscapeDataString(ratingKey.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Put, endpoint, cancellationToken, new StringContent(string.Empty));
		return response.IsSuccessStatusCode;
	}

	public async Task<bool> RemoveFromWatchlistAsync(IMediaId ratingKey, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(RemoveToWatchlistEndpoint, Uri.EscapeDataString(ratingKey.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Put, endpoint, cancellationToken, new StringContent(string.Empty));
		return response.IsSuccessStatusCode;
	}

	private static bool IsIncomplete(PlexTypes.Watchlist.Metadata arg) =>
		arg.Year == 0 || string.IsNullOrEmpty(arg.OriginallyAvailableAt) || string.IsNullOrEmpty(arg.Title);
}
