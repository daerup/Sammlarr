using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Core.Announcing;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Observability.Usage;

namespace Core.Wantlist;

internal sealed class WantlistService(
	IWantlistRepository repository,
	IWantlistOutbox outbox,
	IMediaService media,
	IProviderConfigService providerConfig,
	Announcer announcer,
	ICurrentUser currentUser
) : IWantlistService
{
	public async Task<WantlistSnapshot> GetAsync(CancellationToken cancellationToken = default) => new(await repository.GetAllAsync(cancellationToken));

	public async Task<OutboxSnapshot> GetOutboxAsync(CancellationToken cancellationToken = default)
	{
		return new(await outbox.GetAllAsync(await SinkAsync(cancellationToken), cancellationToken));
	}

	public async Task<WantlistAdded> AddAsync(IEnumerable<MediaId> ids, WantlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var adding = ids.ToList();
		if (adding.Count == 0)
		{
			return new WantlistAdded([], []);
		}

		var sink = await SinkAsync(cancellationToken);
		var queued = new OutboxSnapshot(await outbox.GetAllAsync(sink, cancellationToken));
		var wantlist = await GetAsync(cancellationToken);

		var uncancel = adding.Where(queued.Removes.ContainsKey).ToList();
		var needsPush = adding.Where(id => !wantlist.Contains(id) && !queued.Removes.ContainsKey(id)).ToList();
		var unaddressable = await UnaddressableAsync(needsPush, sink, cancellationToken);

		await outbox.DropAsync(sink, uncancel, cancellationToken);
		var added = await repository.AddAsync(adding.Except(unaddressable), provenance, cancellationToken);
		await outbox.EnqueueAsync(sink, WantlistOperationAction.Add, needsPush.Where(added.Contains).ToList(), cancellationToken);
		return new WantlistAdded(AnnounceAdded(added), unaddressable);
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var removing = ids.ToList();
		if (removing.Count == 0)
		{
			return [];
		}

		var sink = await SinkAsync(cancellationToken);
		var queued = new OutboxSnapshot(await outbox.GetAllAsync(sink, cancellationToken));

		var cancelAdds = removing.Where(queued.Adds.ContainsKey).ToList();
		var pushable = await PushableAsync([.. removing.Where(id => !queued.Adds.ContainsKey(id))], cancellationToken);

		await outbox.DropAsync(sink, cancelAdds, cancellationToken);
		await outbox.EnqueueAsync(sink, WantlistOperationAction.Remove, pushable, cancellationToken);
		return AnnounceRemoved(await repository.RemoveAsync(removing, cancellationToken));
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, FeedId addedBy, CancellationToken cancellationToken = default)
	{
		var removing = ids.ToList();
		if (removing.Count == 0)
		{
			return [];
		}

		var sink = await SinkAsync(cancellationToken);
		var queued = new OutboxSnapshot(await outbox.GetAllAsync(sink, cancellationToken));

		var removed = await repository.RemoveAsync(removing, addedBy, cancellationToken);
		if (removed.Count == 0)
		{
			return removed;
		}

		var cancelAdds = removed.Where(queued.Adds.ContainsKey).ToList();
		var candidates = removed.Where(id => !queued.Adds.ContainsKey(id)).ToList();
		var pushable = await PushableAsync(candidates, cancellationToken);

		await outbox.DropAsync(sink, cancelAdds, cancellationToken);
		await outbox.EnqueueAsync(sink, WantlistOperationAction.Remove, pushable, cancellationToken);
		return AnnounceRemoved(removed);
	}

	public async Task<IReadOnlyList<MediaId>> RequeueAllAsync(SinkKind sink, CancellationToken cancellationToken = default)
	{
		var wantlist = await GetAsync(cancellationToken);
		if (sink == await SinkAsync(cancellationToken))
		{
			await outbox.EnqueueAsync(sink, WantlistOperationAction.Add, wantlist.Ids, cancellationToken);
		}
		else
		{
			await outbox.ReplaceAsync(sink, wantlist.Ids, cancellationToken);
		}

		return [.. wantlist.Ids];
	}

	public Task DropOtherSinksAsync(SinkKind sink, CancellationToken cancellationToken = default) => outbox.DropOtherSinksAsync(sink, cancellationToken);

	private async Task<SinkKind> SinkAsync(CancellationToken cancellationToken) => (await providerConfig.GetAsync(cancellationToken)).Sink;

	private IReadOnlyList<MediaId> AnnounceAdded(IReadOnlyList<MediaId> added)
	{
		if (added.Count > 0)
		{
			announcer.Announce(new WantlistChangedEvent(currentUser.Id, added, []));
		}

		return added;
	}

	private IReadOnlyList<MediaId> AnnounceRemoved(IReadOnlyList<MediaId> removed)
	{
		if (removed.Count > 0)
		{
			announcer.Announce(new WantlistChangedEvent(currentUser.Id, [], removed));
		}

		return removed;
	}

	private async Task<IReadOnlyList<MediaId>> UnaddressableAsync(List<MediaId> ids, SinkKind sink, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return [];
		}
		using var marked = UsageCauses.Mark(UsageCause.WantlistSync);
		return await media.EnsureSinkIdsAsync(ids, sink, cancellationToken);
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
