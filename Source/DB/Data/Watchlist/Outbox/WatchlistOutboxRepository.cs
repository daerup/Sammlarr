using Contracts.Media;
using Contracts.Watchlist.Outbox;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal class WatchlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IMediaIdGroupRepository mediaIdGroupRepository)
	: IWatchlistOutbox
{
	public async Task<IReadOnlyList<WatchlistOperation>> GetAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.WatchlistOutbox.AsNoTracking().Include(o => o.Media).ThenInclude(m => m.MediaIdEntries).ToListAsync(cancellationToken);

		return rows.Select(row => new WatchlistOperation(
				row.Media.MediaIdEntries.Single(e => e.Identifier.Source == mediaProvider).Identifier.ToMediaId(),
				row.Kind
			))
			.ToList();
	}

	public async Task EnqueueAsync(WatchlistOperationKind kind, IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds as IReadOnlyCollection<IMediaId> ?? mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		foreach (var id in ids)
		{
			await mediaIdGroupRepository.UpsertAsync([id], cancellationToken);
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var identifiers = ids.Select(MediaIdentifier.From).ToList();
		var groups = await ctx
			.MediaIdEntries.Where(e => identifiers.Contains(e.Identifier))
			.Select(e => e.MediaIdGroupEntity)
			.Distinct()
			.ToListAsync(cancellationToken);

		var groupIds = groups.Select(g => g.Id).ToList();
		var existingByGroupId = await ctx.WatchlistOutbox.Where(o => groupIds.Contains(o.Media.Id)).ToDictionaryAsync(o => o.Media.Id, cancellationToken);

		foreach (var group in groups)
		{
			if (existingByGroupId.TryGetValue(group.Id, out var existing))
			{
				existing.Kind = kind;
				continue;
			}
			ctx.WatchlistOutbox.Add(new WatchlistOutboxEntity { Media = group, Kind = kind });
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DropAsync(WatchlistOperationKind kind, IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds as IReadOnlyCollection<IMediaId> ?? mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var identifiers = ids.Select(MediaIdentifier.From).ToList();
		var groupIds = await ctx
			.MediaIdEntries.Where(e => identifiers.Contains(e.Identifier))
			.Select(e => e.MediaIdGroupEntity.Id)
			.ToListAsync(cancellationToken);
		if (groupIds.Count == 0)
		{
			return;
		}
		await ctx.WatchlistOutbox.Where(o => groupIds.Contains(o.Media.Id) && o.Kind == kind).ExecuteDeleteAsync(cancellationToken);
	}
}
