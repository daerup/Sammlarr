using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;

namespace Core.Wantlist;

internal sealed class WantlistService(IWantlistRepository repository, IWantlistOutbox outbox, IMediaService media, IProviderConfigService providerConfig)
	: IWantlistService
{
	public async Task<WantlistSnapshot> GetAsync(CancellationToken cancellationToken = default) => new(await repository.GetAllAsync(cancellationToken));

	public async Task<OutboxSnapshot> GetOutboxAsync(CancellationToken cancellationToken = default) => new(await outbox.GetAllAsync(cancellationToken));

	public async Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, WantlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var adding = ids.ToList();
		if (adding.Count == 0)
		{
			return [];
		}

		var queued = await GetOutboxAsync(cancellationToken);
		var wantlist = await GetAsync(cancellationToken);

		var uncancel = adding.Where(queued.Removes.ContainsKey).ToList();
		var needsPush = adding.Where(id => !wantlist.Contains(id) && !queued.Removes.ContainsKey(id)).ToList();

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

		var queued = await GetOutboxAsync(cancellationToken);

		var cancelAdds = removing.Where(queued.Adds.ContainsKey).ToList();
		var needsEnqueue = removing.Where(id => !queued.Adds.ContainsKey(id)).ToList();

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

		var queued = await GetOutboxAsync(cancellationToken);

		var removed = await repository.RemoveAsync(removing, addedBy, cancellationToken);
		if (removed.Count == 0)
		{
			return removed;
		}

		var cancelAdds = removed.Where(queued.Adds.ContainsKey).ToList();
		var candidates = removed.Where(id => !queued.Adds.ContainsKey(id)).ToList();
		var pushable = await PushableAsync(candidates, cancellationToken);

		await outbox.DropAsync(cancelAdds, cancellationToken);
		await outbox.EnqueueAsync(WantlistOperationAction.Remove, pushable, cancellationToken);
		return removed;
	}

	public async Task<IReadOnlyList<MediaId>> RequeueAllAsync(CancellationToken cancellationToken = default)
	{
		var wantlist = await GetAsync(cancellationToken);
		await outbox.EnqueueAsync(WantlistOperationAction.Add, wantlist.Ids, cancellationToken);
		return [.. wantlist.Ids];
	}

	private async Task EnsureSinkIdInExternalsAsync(List<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return;
		}
		var config = await providerConfig.GetAsync(cancellationToken);
		var unaddressable = await media.EnsureSinkIdsAsync(ids, config.Sink, cancellationToken);
		if (unaddressable.Count > 0)
		{
			throw new MissingSinkIdException(config.Sink.ToString());
		}
	}

	private async Task<List<MediaId>> PushableAsync(List<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return [];
		}
		var sinkIds = await media.GetSinkIdsAsync(ids, cancellationToken);
		return [.. ids.Where(sinkIds.ContainsKey)];
	}
}
