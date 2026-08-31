using System.Runtime.CompilerServices;
using Contracts;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Messaging;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Shared;

namespace Core.Watchlist;

internal sealed class WatchlistService(
	IWatchlistRepository repository,
	IWatchlistOutbox outbox,
	IMediaService media,
	IMediaRepository mediaRepository,
	IProviderConfigRepository configRepository,
	IMessagePublisher publisher,
	ICurrentUser currentUser,
	IDateTimeProvider clock
) : IWatchlistService
{
	public async IAsyncEnumerable<WatchlistedMedia> GetWatchlistItemsAsync(
		WatchlistQueryOptions options,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var entries = (await repository.GetEntriesAsync(cancellationToken)).ToList();

		if (options.IncludeRemoved)
		{
			var removals = (await outbox.GetAsync(cancellationToken))
				.Where(op => op.Action == WatchlistOperationAction.Remove)
				.Select(op => new WatchlistEntry(op.MediaId, op.AddedAt, new ManualOrigin(ManualSource.Unknown)));
			entries = [.. entries, .. removals];
		}

		var ordered = entries.OrderByDescending(entry => entry.AddedAt).ToList();
		var pipeline = media.GetDetailsAsync(ordered.Select(entry => entry.MediaId), cancellationToken).ZipAwait(ordered, cancellationToken);

		await foreach (var (detail, entry) in pipeline.WhereSuccessAwait().WithCancellation(cancellationToken))
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
		var pendingRemoves = ops.Where(o => o.Action == WatchlistOperationAction.Remove).Select(o => o.MediaId).ToHashSet();
		var existing = (await repository.GetIdsAsync(cancellationToken)).ToHashSet();

		var uncancel = ids.Where(pendingRemoves.Contains).ToList();
		var needsPush = ids.Where(id => !existing.Contains(id) && !pendingRemoves.Contains(id)).ToList();

		await EnsureWatchlistProviderInExternalsAsync(needsPush, cancellationToken);

		await outbox.DropAsync(uncancel, cancellationToken);
		var added = await repository.AddAsync(ids, provenance, cancellationToken);
		await outbox.EnqueueAsync(WatchlistOperationAction.Add, needsPush, cancellationToken);

		if (added.Count > 0)
		{
			await publisher.PublishAsync(new WatchlistItemsAddedEvent(currentUser.Id, clock.Now, added), cancellationToken);
		}
	}

	public async Task RemoveFromWatchlistAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var unpushedAdds = ops.Where(o => o.Action == WatchlistOperationAction.Add).Select(o => o.MediaId).ToHashSet();

		var cancelAdds = ids.Where(unpushedAdds.Contains).ToList();
		var needsEnqueue = ids.Where(id => !unpushedAdds.Contains(id)).ToList();

		await EnsureWatchlistProviderInExternalsAsync(needsEnqueue, cancellationToken);

		if (cancelAdds.Count > 0)
		{
			await outbox.DropAsync(cancelAdds, cancellationToken);
		}
		if (needsEnqueue.Count > 0)
		{
			await outbox.EnqueueAsync(WatchlistOperationAction.Remove, needsEnqueue, cancellationToken);
		}
		var removed = await repository.RemoveAsync(ids, cancellationToken);

		if (removed.Count > 0)
		{
			await publisher.PublishAsync(new WatchlistItemsRemovedEvent(currentUser.Id, clock.Now, removed), cancellationToken);
		}
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
