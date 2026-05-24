using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Watchlist;
using Shared;

namespace API.Plex;

internal class PlexWatchListProvider(PlexClient plexClient) : IWatchlistProvider
{
	private const string AddToWatchlistEndpoint = "https://vod.provider.plex.tv/actions/addToWatchlist?ratingKey={0}";
	private const string RemoveToWatchlistEndpoint = "https://vod.provider.plex.tv/actions/removeFromWatchlist?ratingKey={0}";
	private const string WatchlistEndpoint = "https://discover.provider.plex.tv/library/sections/watchlist/all";

	public async IAsyncEnumerable<IExternalMediaId> GetWatchlistAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var (items, pages) = await FetchPageAsync(1, cancellationToken);
		foreach (var id in items)
		{
			yield return id;
		}

		if (pages <= 1)
		{
			yield break;
		}

		var rest = Enumerable
			.Range(2, pages - 1)
			.ToAsyncEnumerable()
			.SelectAwait(async p => await FetchPageAsync(p, cancellationToken), IWatchlistService.RecommendedConcurrency, cancellationToken);

		await foreach (var (remaining, _) in rest)
		{
			foreach (var id in remaining)
			{
				yield return id;
			}
		}
	}

	public async IAsyncEnumerable<WatchlistPushResult> AddToWatchlistAsync(
		IEnumerable<IExternalMediaId> ids,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
            yield return await PushAsync(AddToWatchlistEndpoint, id, cancellationToken);
		}
	}

	public async IAsyncEnumerable<WatchlistPushResult> RemoveFromWatchlistAsync(
		IEnumerable<IExternalMediaId> ids,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
            yield return await PushAsync(RemoveToWatchlistEndpoint, id, cancellationToken);
		}
	}
	private async Task<WatchlistPushResult> PushAsync(
        string endpoint,
		IExternalMediaId id,
        CancellationToken cancellationToken
	)
	{
        var formattedEndpoint = string.Format(endpoint, Uri.EscapeDataString(id.Value));
        try
        {
            using var response = await plexClient.SendAsync(HttpMethod.Put, formattedEndpoint, cancellationToken, new StringContent(string.Empty));
            return new WatchlistPushResult(id, response.IsSuccessStatusCode);
        }
        catch (Exception)
        {
            return new WatchlistPushResult(id, false);
        }
	}

	private async Task<(List<IExternalMediaId>, int)> FetchPageAsync(int page, CancellationToken cancellationToken)
	{
		const string containerStartHeader = "X-Plex-Container-Start";
		var offset = (page - 1) * PlexClient.ContainerSize;
		var response = await plexClient.SendAsync(HttpMethod.Get, WatchlistEndpoint, cancellationToken, null, (containerStartHeader, offset.ToString()));
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result =
			await JsonSerializer.DeserializeAsync<PlexTypes.Watchlist.Root>(stream, cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Plex watchlist response was empty.");

		var items = result.MediaContainer.Metadata.Where(m => !IsIncomplete(m)).Select(m => m.PlexId).Cast<IExternalMediaId>().ToList();
		var totalPages = result.MediaContainer.TotalSize <= 0 ? 1 : (int)Math.Ceiling(result.MediaContainer.TotalSize / (double)PlexClient.ContainerSize);

		return (items, totalPages);
	}

	private static bool IsIncomplete(PlexTypes.Watchlist.Metadata arg) =>
		arg.Year == 0 || string.IsNullOrEmpty(arg.OriginallyAvailableAt) || string.IsNullOrEmpty(arg.Title);
}
