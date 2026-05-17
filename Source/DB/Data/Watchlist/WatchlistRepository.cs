using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist;

internal class WatchlistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IMediaIdGroupRepository mediaIdGroupRepository) : IWatchlistRepository
{
	public async Task<IReadOnlyList<WatchlistEntry>> GetEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.WatchlistEntries.AsNoTracking()
			.Include(w => w.Media)
				.ThenInclude(m => m.MediaIdEntries)
			.Include(w => w.SourceFeed)
			.ToListAsync(cancellationToken);

		return rows.Select(row => new WatchlistEntry(
				row.Media.MediaIdEntries.Single(e => e.Identifier.Source == mediaProvider).Identifier.ToMediaId(),
				row.CreatedAt,
				row.ToProvenance()
			))
			.ToList();
	}

	public async Task<IReadOnlyList<IMediaId>> GetIdsAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.WatchlistEntries.AsNoTracking().Include(w => w.Media).ThenInclude(m => m.MediaIdEntries).ToListAsync(cancellationToken);

		return rows.Select(w => w.Media.MediaIdEntries.Single(e => e.Identifier.Source == mediaProvider).Identifier.ToMediaId()).ToList();
	}

	public async Task AddAsync(IEnumerable<IMediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default)
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
		var existingGroupIds = await ctx.WatchlistEntries.Where(w => groupIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToHashSetAsync(cancellationToken);

		foreach (var group in groups.Where(g => !existingGroupIds.Contains(g.Id)))
		{
			var row = new WatchlistEntryEntity { Media = group };
			row.Apply(provenance);
			ctx.WatchlistEntries.Add(row);
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds as IReadOnlyCollection<IMediaId> ?? mediaIds.ToList();
		if (ids.Count == 0)
		{
			return;
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var groupIds = await ResolveGroupIdsAsync(ctx, ids, cancellationToken);
		if (groupIds.Count == 0)
		{
			return;
		}
		await ctx.WatchlistEntries.Where(w => groupIds.Contains(w.Media.Id)).ExecuteDeleteAsync(cancellationToken);
	}

	private static async Task<IReadOnlyList<Guid>> ResolveGroupIdsAsync(
		SammlarrDbContext ctx,
		IReadOnlyCollection<IMediaId> ids,
		CancellationToken cancellationToken
	)
	{
		var identifiers = ids.Select(MediaIdentifier.From).ToList();
		return await ctx.MediaIdEntries.Where(e => identifiers.Contains(e.Identifier)).Select(e => e.MediaIdGroupEntity.Id).ToListAsync(cancellationToken);
	}
}
