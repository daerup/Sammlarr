using System.Runtime.CompilerServices;
using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Shared;

namespace Core.Watchlist;

internal class WatchlistService(
	IProviderConfigRepository configRepository,
	IWatchlistRepository repository,
	IWatchlistOutbox outbox,
	IMediaMetadataProvider metadataProvider
) : IWatchlistService
{
	public async IAsyncEnumerable<WatchlistedItem> GetWatchlistItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var mediaProvider = config.WatchlistProvider.AsMediaProvider();

		var entries = await repository.GetEntriesAsync(mediaProvider, cancellationToken);
		var pipeline = entries
			.ToAsyncEnumerable()
			.OrderByDescending(e => e.AddedAt)
			.SelectAwait(
				async e => (e, await metadataProvider.GetAsync(e.Id, cancellationToken)),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var (entry, detail) in pipeline)
		{
			yield return new WatchlistedItem(detail, entry.AddedAt, entry.Provenance);
		}
	}

	public async Task<IReadOnlySet<IMediaId>> GetWatchlistIdsAsync(CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var entries = await repository.GetEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
		return entries.OrderBy(e => e.AddedAt).Select(e => e.Id).ToHashSet();
	}

	public async Task AddToWatchlistAsync(IEnumerable<IMediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds as IReadOnlyCollection<IMediaId> ?? mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var config = await configRepository.GetAsync(cancellationToken);
		var mediaProvider = config.WatchlistProvider.AsMediaProvider();

		var ops = await outbox.GetAsync(mediaProvider, cancellationToken);
		var pendingRemoves = ops.Where(o => o.Kind == WatchlistOperationKind.Remove).Select(o => o.MediaId).ToHashSet();
		var existing = (await repository.GetIdsAsync(mediaProvider, cancellationToken)).ToHashSet();

		var uncancel = ids.Where(pendingRemoves.Contains).ToList();
		var needsPush = ids.Where(id => !existing.Contains(id) && !pendingRemoves.Contains(id)).ToList();

		await outbox.DropAsync(WatchlistOperationKind.Remove, uncancel, cancellationToken);
		await repository.AddAsync(ids, provenance, cancellationToken);
		await outbox.EnqueueAsync(WatchlistOperationKind.Add, needsPush, cancellationToken);
	}

	public async Task RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var mediaProvider = config.WatchlistProvider.AsMediaProvider();

		var ops = await outbox.GetAsync(mediaProvider, cancellationToken);
		var hadUnpushedAdd = ops.Any(o => o.Kind == WatchlistOperationKind.Add && o.MediaId.Equals(mediaId));

		if (hadUnpushedAdd)
		{
			await outbox.DropAsync(WatchlistOperationKind.Add, [mediaId], cancellationToken);
			await repository.RemoveAsync([mediaId], cancellationToken);
			return;
		}

		await outbox.EnqueueAsync(WatchlistOperationKind.Remove, [mediaId], cancellationToken);
		await repository.RemoveAsync([mediaId], cancellationToken);
	}
}
