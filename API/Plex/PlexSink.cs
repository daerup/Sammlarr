using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace API.Plex;

internal class PlexSink(PlexClient plexClient, PlexUrls urls, IMediaService mediaService, ILogger<PlexSink> logger) : IMediaSink
{
	public SinkKind Kind => SinkKind.Plex;

	public async Task<SinkContents> ReadAsync(CancellationToken cancellationToken = default)
	{
		var entries = await WatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
		var requests = entries.Select(entry => new ResolveRequest([.. entry.Externals], MediaProvider.Plex, new None()));
		List<MediaId> matched = [];
		List<UnmatchedMedia> unmatched = [];
		await foreach (var (result, entry) in mediaService.IdentifyAsync(requests, cancellationToken).ZipAwait(entries, cancellationToken))
		{
			if (result is Success<MediaIdentity>(var identity))
			{
				matched.Add(identity.Id);
			}
			else
			{
				unmatched.Add(entry);
				logger.LogWarning("Plex watchlist item {Title} could not be matched during sync", entry.Title ?? entry.Externals.FirstOrDefault()?.Value);
			}
		}

		return new SinkContents(matched, unmatched);
	}

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
			.SelectAwait(async p => await FetchPageAsync(p, cancellationToken), IWantlistService.RecommendedConcurrency, cancellationToken);

		await foreach (var (remaining, _) in rest)
		{
			foreach (var item in remaining)
			{
				yield return item;
			}
		}
	}

	public async IAsyncEnumerable<SinkSyncAck> AddToWantlistAsync(
		IEnumerable<IExternalMediaId> ids,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return await PushAsync(urls.AddToWatchlist(id.Value), id, cancellationToken);
		}
	}

	public async IAsyncEnumerable<SinkSyncAck> RemoveFromWantlistAsync(
		IEnumerable<IExternalMediaId> ids,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return await PushAsync(urls.RemoveFromWatchlist(id.Value), id, cancellationToken);
		}
	}

	private async Task<SinkSyncAck> PushAsync(string formattedEndpoint, IExternalMediaId id, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await plexClient.SendAsync(HttpMethod.Put, formattedEndpoint, cancellationToken, new StringContent(string.Empty));
			if (!response.IsSuccessStatusCode)
			{
				logger.LogWarning("Plex watchlist push failed ({StatusCode}) for {ExternalId} at {Endpoint}", (int)response.StatusCode, id.Value, formattedEndpoint);
			}
			return new SinkSyncAck(id, response.IsSuccessStatusCode);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Plex watchlist push threw for {ExternalId} at {Endpoint}", id.Value, formattedEndpoint);
			return new SinkSyncAck(id, false);
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

		var items = (result.MediaContainer.Metadata ?? []).Select(Entry).ToList();
		var totalPages = result.MediaContainer.TotalSize <= 0 ? 1 : (int)Math.Ceiling(result.MediaContainer.TotalSize / (double)PlexClient.ContainerSize);

		return (items, totalPages);
	}

	private static UnmatchedMedia Entry(PlexTypes.Watchlist.Metadata metadata) =>
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
