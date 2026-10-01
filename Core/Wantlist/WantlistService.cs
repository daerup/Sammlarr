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
	IMediaRepository mediaRepository,
	IProviderConfigService providerConfig
) : IWantlistService
{
	public async Task<WantlistSnapshot> GetAsync(CancellationToken cancellationToken = default) => new(await repository.GetAllAsync(cancellationToken));

	public Task<IReadOnlyDictionary<FeedId, int>> GetOwnedCountsAsync(CancellationToken cancellationToken = default) =>
		repository.CountByFeedAsync(cancellationToken);

	public async Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, WantlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var adding = ids.ToList();
		if (adding.Count == 0)
		{
			return [];
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var pendingRemoves = ops.Where(o => o.Action == WantlistOperationAction.Remove).Select(o => o.MediaId).ToHashSet();
		var wantlist = await GetAsync(cancellationToken);

		var uncancel = adding.Where(pendingRemoves.Contains).ToList();
		var needsPush = adding.Where(id => !wantlist.Contains(id) && !pendingRemoves.Contains(id)).ToList();

		await EnsureSinkIdInExternalsAsync(needsPush, cancellationToken);

		await outbox.DropAsync(uncancel, cancellationToken);
		var added = await repository.AddAsync(adding, provenance, cancellationToken);
		await outbox.EnqueueAsync(WantlistOperationAction.Add, needsPush.Where(added.Contains).ToList(), cancellationToken);
		return added;
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var removing = ids.ToList();
		if (removing.Count == 0)
		{
			return [];
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var unpushedAdds = ops.Where(o => o.Action == WantlistOperationAction.Add).Select(o => o.MediaId).ToHashSet();

		var cancelAdds = removing.Where(unpushedAdds.Contains).ToList();
		var needsEnqueue = removing.Where(id => !unpushedAdds.Contains(id)).ToList();

		await EnsureSinkIdInExternalsAsync(needsEnqueue, cancellationToken);

		if (cancelAdds.Count > 0)
		{
			await outbox.DropAsync(cancelAdds, cancellationToken);
		}
		if (needsEnqueue.Count > 0)
		{
			await outbox.EnqueueAsync(WantlistOperationAction.Remove, needsEnqueue, cancellationToken);
		}
		return await repository.RemoveAsync(removing, cancellationToken);
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, FeedId addedBy, CancellationToken cancellationToken = default)
	{
		var removing = ids.ToList();
		if (removing.Count == 0)
		{
			return [];
		}

		var ops = await outbox.GetAsync(cancellationToken);
		var unpushedAdds = ops.Where(o => o.Action == WantlistOperationAction.Add).Select(o => o.MediaId).ToHashSet();

		var removed = await repository.RemoveAsync(removing, addedBy, cancellationToken);
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
