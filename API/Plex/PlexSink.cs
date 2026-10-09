using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace API.Plex;

internal class PlexSink(PlexClient plexClient, PlexUrls urls, IMediaService mediaService, ILogger<PlexSink> logger) : IMediaSink
{
	private const int PageConcurrency = 10;

	private static readonly Error OwnWatchlist = Error.From("A Plex watchlist belongs to its Plex account, so Sammlarr leaves it as it is.");

	public SinkKind Kind => SinkKind.Plex;

	public Optional<MediaProvider> AddressedBy(MediaType type) => Some.Of(MediaProvider.Plex);

	public async Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default)
	{
		var items = await WatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
		var requests = items.Select(item => new IdentifyRequest([.. item.Externals], new None()));
		List<MediaId> matched = [];
		List<UnmatchedMedia> unmatched = [];
		await foreach (var (result, item) in mediaService.IdentifyAsync(requests, cancellationToken).ZipAwait(items, cancellationToken))
		{
			result
				.Apply(identity => matched.Add(identity.Id))
				.ApplyError(_ => unmatched.Add(item.Unmatched()))
				.ApplyError(_ =>
					logger.LogWarning("Plex watchlist item {Title} could not be matched during sync", item.Details.Title ?? item.Externals.FirstOrDefault()?.Value)
				);
		}

		return new SinkSnapshot(matched, unmatched);
	}

	public async IAsyncEnumerable<SinkSyncAck> ApplyAsync(
		WantlistOperationAction action,
		IReadOnlyList<SinkPush> pushes,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var ids = pushes.Select(push => push.MediaId).ToList();
		var plexIds = await mediaService.GetSinkIdsAsync(ids, cancellationToken);
		foreach (var id in ids)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return await plexIds.GetValueOrNone(id).Match(plexId => PushAsync(id, action, plexId, cancellationToken), () => Task.FromResult(Unaddressable(id)));
		}
	}

	public Task<Result<SinkHoldings>> HoldingsOfAsync(User person, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<Result<SinkHoldings>>(OwnWatchlist);
	}

	public Task<Result<int>> DeleteTitlesOfAsync(User person, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<Result<int>>(OwnWatchlist);
	}

	private string Endpoint(WantlistOperationAction action, IExternalMediaId plexId) =>
		action switch
		{
			WantlistOperationAction.Add => urls.AddToWatchlist(plexId.Value),
			WantlistOperationAction.Remove => urls.RemoveFromWatchlist(plexId.Value),
		};

	private async IAsyncEnumerable<FoundMedia> WatchlistAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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

	private async Task<SinkSyncAck> PushAsync(MediaId id, WantlistOperationAction action, IExternalMediaId plexId, CancellationToken cancellationToken)
	{
		var endpoint = Endpoint(action, plexId);
		try
		{
			using var response = await plexClient.SendAsync(HttpMethod.Put, endpoint, cancellationToken, new StringContent(string.Empty));
			if (response.IsSuccessStatusCode)
			{
				return new SinkSyncAck(id, SinkSyncOutcome.Applied);
			}

			logger.LogWarning("Plex watchlist push failed ({StatusCode}) for {ExternalId} at {Endpoint}", (int)response.StatusCode, plexId.Value, endpoint);
			return Failed(id, $"Plex answered {(int)response.StatusCode} {response.StatusCode}.");
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Plex watchlist push threw for {ExternalId} at {Endpoint}", plexId.Value, endpoint);
			return Failed(id, $"Plex could not be reached: {ex.Message}");
		}
	}

	private static SinkSyncAck Failed(MediaId id, string reason) => new(id, SinkSyncOutcome.Failed) { Reason = Some.Of(reason) };

	private static SinkSyncAck Unaddressable(MediaId id) =>
		new(id, SinkSyncOutcome.Unaddressable) { Reason = Some.Of("Sammlarr found no Plex id for this title.") };

	private async Task<(List<FoundMedia>, int)> FetchPageAsync(int page, CancellationToken cancellationToken)
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

	private static FoundMedia Item(PlexTypes.Watchlist.Metadata metadata)
	{
		return new(
			[metadata.PlexId],
			MediaDetails.Empty(metadata.PlexId.Type) with
			{
				Title = ProviderValues.Text(metadata.Title),
				ReleaseDate = ReleasedOn(metadata),
			}
		);
	}

	private static DateOnly? ReleasedOn(PlexTypes.Watchlist.Metadata metadata)
	{
		return ProviderValues.Date(metadata.OriginallyAvailableAt) ?? ProviderValues.YearStart(metadata.Year);
	}
}
