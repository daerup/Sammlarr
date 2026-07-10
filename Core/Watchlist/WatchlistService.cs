using System.Runtime.CompilerServices;
using Contracts;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Shared;

namespace Core.Watchlist;

internal class WatchlistService(
	IWatchlistRepository repository,
	IWatchlistOutbox outbox,
	IMediaService media,
	IMediaRepository mediaRepository,
	IProviderConfigRepository configRepository
) : IWatchlistService
{
	public async IAsyncEnumerable<WatchlistedMedia> GetWatchlistItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var entries = (await repository.GetEntriesAsync(cancellationToken)).OrderByDescending(e => e.AddedAt).ToList();
		var pipeline = media.GetDetailsAsync(entries.Select(e => e.MediaId), cancellationToken).Zip(entries, cancellationToken);

		await foreach (var (detail, entry) in pipeline.WhereSuccess())
		{
			yield return new WatchlistedMedia(detail, entry.AddedAt, entry.Provenance);
		}
	}

	public async Task<IReadOnlySet<MediaId>> GetWatchlistIdsAsync(CancellationToken cancellationToken = default)
	{
		var entries = await repository.GetEntriesAsync(cancellationToken);
		return entries.OrderBy(e => e.AddedAt).Select(e => e.MediaId).ToHashSet();
	}

	public async Task AddToWatchlistAsync(IEnumerable<MediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var pendingRemoves = ops.Where(o => o.Kind == WatchlistOperationKind.Remove).Select(o => o.MediaId).ToHashSet();
		var existing = (await repository.GetIdsAsync(cancellationToken)).ToHashSet();

		var uncancel = ids.Where(pendingRemoves.Contains).ToList();
		var needsPush = ids.Where(id => !existing.Contains(id) && !pendingRemoves.Contains(id)).ToList();

		await EnsureWatchlistProviderInExternalsAsync(needsPush, cancellationToken);

		await outbox.DropAsync(WatchlistOperationKind.Remove, uncancel, cancellationToken);
		await repository.AddAsync(ids, provenance, cancellationToken);
		await outbox.EnqueueAsync(WatchlistOperationKind.Add, needsPush, cancellationToken);
	}

	public async Task RemoveFromWatchlistAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var unpushedAdds = ops.Where(o => o.Kind == WatchlistOperationKind.Add).Select(o => o.MediaId).ToHashSet();

		var cancelAdds = ids.Where(unpushedAdds.Contains).ToList();
		var needsEnqueue = ids.Where(id => !unpushedAdds.Contains(id)).ToList();

		await EnsureWatchlistProviderInExternalsAsync(needsEnqueue, cancellationToken);

		if (cancelAdds.Count > 0)
		{
			await outbox.DropAsync(WatchlistOperationKind.Add, cancelAdds, cancellationToken);
		}
		if (needsEnqueue.Count > 0)
		{
			await outbox.EnqueueAsync(WatchlistOperationKind.Remove, needsEnqueue, cancellationToken);
		}
		await repository.RemoveAsync(ids, cancellationToken);
	}

	private async Task EnsureWatchlistProviderInExternalsAsync(List<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return;
		}
		var config = await configRepository.GetAsync(cancellationToken);
		var watchlistProvider = config.WatchlistProvider.AsMediaProvider();
		var externals = await mediaRepository.GetExternalsAsync(ids, watchlistProvider, cancellationToken);
		if (ids.Any(id => !externals.ContainsKey(id)))
		{
			throw new MissingWatchlistExternalIdException(watchlistProvider);
		}
	}
}
