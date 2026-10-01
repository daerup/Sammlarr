using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace API.Plex;

internal class PlexSink(PlexClient plexClient, PlexUrls urls, IMediaService mediaService, ILogger<PlexSink> logger) : IMediaSink
{
	private const int PageConcurrency = 10;

	public SinkKind Kind => SinkKind.Plex;

	public async Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default)
	{
		var items = await WatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
		var requests = items.Select(item => new ResolveRequest([.. item.Externals], MediaProvider.Plex, new None()));
		List<MediaId> matched = [];
		List<UnmatchedMedia> unmatched = [];
		await foreach (var (result, item) in mediaService.IdentifyAsync(requests, cancellationToken).ZipAwait(items, cancellationToken))
		{
			if (result is Success<MediaIdentity>(var identity))
			{
				matched.Add(identity.Id);
			}
			else
			{
				unmatched.Add(item);
				logger.LogWarning("Plex watchlist item {Title} could not be matched during sync", item.Title ?? item.Externals.FirstOrDefault()?.Value);
			}
		}

		return new SinkSnapshot(matched, unmatched);
	}

	public async IAsyncEnumerable<SinkSyncAck> ApplyAsync(
		WantlistOperationAction action,
		IReadOnlyList<MediaId> ids,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var plexIds = await mediaService.GetExternalsAsync(ids, MediaProvider.Plex, cancellationToken);
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var outcome = plexIds.TryGetValue(id, out var plexId) ? await PushAsync(action, plexId, cancellationToken) : SinkSyncOutcome.Unaddressable;
			yield return new SinkSyncAck(id, outcome);
		}
	}

	private string Endpoint(WantlistOperationAction action, IExternalMediaId plexId) =>
		action switch
		{
			WantlistOperationAction.Add => urls.AddToWatchlist(plexId.Value),
			WantlistOperationAction.Remove => urls.RemoveFromWatchlist(plexId.Value),
		};

	private async IAsyncEnumerable<UnmatchedMedia> WatchlistAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var (items, pages) = await FetchPageAsync(1, cancellationToken);
		foreach (var item in items)
		{
			yield return item;
		}

		if (pages <= 1)
		{
			yield break;
		}

		var rest = Enumerable
			.Range(2, pages - 1)
			.ToAsyncEnumerable()
			.SelectAwait(async page => await FetchPageAsync(page, cancellationToken), PageConcurrency, cancellationToken);

		await foreach (var (remaining, _) in rest)
		{
			foreach (var item in remaining)
			{
				yield return item;
			}
		}
	}

	private async Task<SinkSyncOutcome> PushAsync(WantlistOperationAction action, IExternalMediaId plexId, CancellationToken cancellationToken)
	{
		var endpoint = Endpoint(action, plexId);
		try
		{
			using var response = await plexClient.SendAsync(HttpMethod.Put, endpoint, cancellationToken, new StringContent(string.Empty));
			if (!response.IsSuccessStatusCode)
			{
				logger.LogWarning("Plex watchlist push failed ({StatusCode}) for {ExternalId} at {Endpoint}", (int)response.StatusCode, plexId.Value, endpoint);
			}
			return response.IsSuccessStatusCode ? SinkSyncOutcome.Applied : SinkSyncOutcome.Failed;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Plex watchlist push threw for {ExternalId} at {Endpoint}", plexId.Value, endpoint);
			return SinkSyncOutcome.Failed;
		}
	}

	private async Task<(List<UnmatchedMedia>, int)> FetchPageAsync(int page, CancellationToken cancellationToken)
	{
		const string containerStartHeader = "X-Plex-Container-Start";
		var offset = (page - 1) * PlexClient.ContainerSize;
		var response = await plexClient.SendAsync(HttpMethod.Get, urls.Watchlist, cancellationToken, null, (containerStartHeader, offset.ToString()));
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result =
			await JsonSerializer.DeserializeAsync<PlexTypes.Watchlist.Root>(stream, cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Plex watchlist response was empty.");

		var items = (result.MediaContainer.Metadata ?? []).Select(Item).ToList();
		var totalPages = result.MediaContainer.TotalSize <= 0 ? 1 : (int)Math.Ceiling(result.MediaContainer.TotalSize / (double)PlexClient.ContainerSize);

		return (items, totalPages);
	}

	private static UnmatchedMedia Item(PlexTypes.Watchlist.Metadata metadata) =>
		new()
		{
			Externals = [metadata.PlexId],
			Title = metadata.Title.NullIfBlank(),
			ReleaseDate = ReleasedOn(metadata),
		};

	private static DateOnly? ReleasedOn(PlexTypes.Watchlist.Metadata metadata) =>
		DateOnly.TryParse(metadata.OriginallyAvailableAt, out var released) ? released
		: metadata.Year > 0 ? new DateOnly(metadata.Year, 1, 1)
		: null;
}
