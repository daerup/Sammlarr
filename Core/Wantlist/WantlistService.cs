using System.Runtime.CompilerServices;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;

namespace Core.Wantlist;

internal sealed class WantlistService(
	IWantlistRepository repository,
	IWantlistOutbox outbox,
	IMediaService media,
	IMediaRepository mediaRepository,
	IProviderConfigService providerConfig
) : IWantlistService
{
	public async IAsyncEnumerable<WantlistedMedia> GetWantlistItemsAsync(
		WantlistQueryOptions options,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var entries = (await repository.GetEntriesAsync(cancellationToken)).ToList();

		if (options.IncludeRemoved)
		{
			var removals = (await outbox.GetAsync(cancellationToken))
				.Where(op => op.Action == WantlistOperationAction.Remove)
				.Select(op => new WantlistItem(op.MediaId, op.AddedAt, new ManualOrigin(ManualSource.Unknown)));
			entries = [.. entries, .. removals];
		}

		var ordered = entries.OrderByDescending(entry => entry.AddedAt).ToList();
		var pipeline = media.GetDetailsAsync(ordered.Select(entry => entry.MediaId), cancellationToken).ZipAwait(ordered, cancellationToken);

		await foreach (var (detail, entry) in pipeline.WhereSuccessAwait().WithCancellation(cancellationToken))
		{
			yield return new WantlistedMedia(detail, entry.AddedAt, entry.Provenance);
		}
	}

	public async Task<IReadOnlySet<MediaId>> GetWantlistIdsAsync(CancellationToken cancellationToken = default) =>
		(await repository.GetIdsAsync(cancellationToken)).ToHashSet();

	public Task<IReadOnlyDictionary<FeedId, int>> GetOwnedCountsAsync(CancellationToken cancellationToken = default) =>
		repository.GetOwnedCountsAsync(cancellationToken);

	public async Task<IReadOnlyDictionary<MediaId, WantlistItem>> GetEntriesAsync(CancellationToken cancellationToken = default) =>
		(await repository.GetEntriesAsync(cancellationToken)).ToDictionary(entry => entry.MediaId);

	public async Task<Optional<WantlistItem>> GetEntryAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var entries = await repository.GetEntriesAsync(cancellationToken);
		return entries.FirstOrDefault(entry => entry.MediaId == mediaId) is { } match ? Some.Of(match) : new None();
	}

	public async Task<IReadOnlyList<MediaId>> AddToWantlistAsync(
		IEnumerable<MediaId> mediaIds,
		WantlistItemProvenance provenance,
		CancellationToken cancellationToken = default
	)
	{
		var ids = mediaIds.ToList();
		if (ids.Count == 0)
		{
			return [];
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var pendingRemoves = ops.Where(o => o.Action == WantlistOperationAction.Remove).Select(o => o.MediaId).ToHashSet();
		var existing = (await repository.GetIdsAsync(cancellationToken)).ToHashSet();

		var uncancel = ids.Where(pendingRemoves.Contains).ToList();
		var needsPush = ids.Where(id => !existing.Contains(id) && !pendingRemoves.Contains(id)).ToList();

		await EnsureSinkIdInExternalsAsync(needsPush, cancellationToken);

		await outbox.DropAsync(uncancel, cancellationToken);
		var added = await repository.AddAsync(ids, provenance, cancellationToken);
		await outbox.EnqueueAsync(WantlistOperationAction.Add, needsPush.Where(added.Contains).ToList(), cancellationToken);
		return added;
	}

	public async Task<IReadOnlyList<MediaId>> RemoveOwnedAsync(IEnumerable<MediaId> mediaIds, FeedId owner, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.ToList();
		if (ids.Count == 0)
		{
			return [];
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var unpushedAdds = ops.Where(o => o.Action == WantlistOperationAction.Add).Select(o => o.MediaId).ToHashSet();

		var removed = await repository.RemoveOwnedAsync(ids, owner, cancellationToken);
		if (removed.Count == 0)
		{
			return removed;
		}

		var cancelAdds = removed.Where(unpushedAdds.Contains).ToList();
		var candidates = removed.Where(id => !unpushedAdds.Contains(id)).ToList();
		var pushable = await PushableAsync(candidates, cancellationToken);

		await outbox.DropAsync(cancelAdds, cancellationToken);
		await outbox.EnqueueAsync(WantlistOperationAction.Remove, pushable, cancellationToken);
		return removed;
	}

	public async Task RemoveFromWantlistAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var unpushedAdds = ops.Where(o => o.Action == WantlistOperationAction.Add).Select(o => o.MediaId).ToHashSet();

		var cancelAdds = ids.Where(unpushedAdds.Contains).ToList();
		var needsEnqueue = ids.Where(id => !unpushedAdds.Contains(id)).ToList();

		await EnsureSinkIdInExternalsAsync(needsEnqueue, cancellationToken);

		if (cancelAdds.Count > 0)
		{
			await outbox.DropAsync(cancelAdds, cancellationToken);
		}
		if (needsEnqueue.Count > 0)
		{
			await outbox.EnqueueAsync(WantlistOperationAction.Remove, needsEnqueue, cancellationToken);
		}
		await repository.RemoveAsync(ids, cancellationToken);
	}

	private async Task EnsureSinkIdInExternalsAsync(List<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return;
		}
		var config = await providerConfig.GetAsync(cancellationToken);
		var sinkProvider = config.Sink.AsMediaProvider();
		var externals = await mediaRepository.GetExternalsAsync(ids, sinkProvider, cancellationToken);
		if (ids.Any(id => !externals.ContainsKey(id)))
		{
			throw new MissingSinkIdException(sinkProvider);
		}
	}

	private async Task<List<MediaId>> PushableAsync(List<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return [];
		}
		var config = await providerConfig.GetAsync(cancellationToken);
		var externals = await mediaRepository.GetExternalsAsync(ids, config.Sink.AsMediaProvider(), cancellationToken);
		return [.. ids.Where(externals.ContainsKey)];
	}
}
