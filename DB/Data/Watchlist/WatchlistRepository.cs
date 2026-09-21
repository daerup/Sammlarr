using Contracts.Watchlist;
using Domain.Feed;
using Domain.Media;
using Domain.Watchlist;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist;

internal sealed class WatchlistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWatchlistRepository
{
	public async Task<IReadOnlyList<WatchlistEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from watchlistEntry in ctx.Watchlist.AsNoTracking().Include(w => w.SourceFeed)
			select new { WatchlistEntry = watchlistEntry, MediaIdValue = watchlistEntry.Media.Id }
		).ToListAsync(cancellationToken);

		return [.. rows.Select(row => new WatchlistEntry(MediaId.From(row.MediaIdValue), row.WatchlistEntry.CreatedAt, row.WatchlistEntry.ToProvenance()))];
	}

	public async Task<IReadOnlyList<MediaId>> GetIdsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var values = await ctx.Watchlist.AsNoTracking().Select(w => w.Media.Id).ToListAsync(cancellationToken);
		return [.. values.Select(MediaId.From)];
	}

	public async Task<IReadOnlyList<MediaId>> AddAsync(
		IEnumerable<MediaId> mediaIds,
		WatchlistItemProvenance provenance,
		CancellationToken cancellationToken = default
	)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return [];
		}

		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var matchedMedia = await ctx.Media.Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var existingMediaIds = await ctx.Watchlist.Where(w => rawIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToHashSetAsync(cancellationToken);

		var inserted = matchedMedia.Where(m => !existingMediaIds.Contains(m.Id)).ToList();
		foreach (var media in inserted)
		{
			ctx.Watchlist.Add(WatchlistEntryMapping.ToRow(media, provenance));
		}

		await ctx.SaveChangesAsync(cancellationToken);
		return [.. inserted.Select(m => MediaId.From(m.Id))];
	}

	public async Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return [];
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var removed = await ctx.Watchlist.Where(w => rawIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToListAsync(cancellationToken);
		await ctx.Watchlist.Where(w => rawIds.Contains(w.Media.Id)).ExecuteDeleteAsync(cancellationToken);
		return [.. removed.Select(MediaId.From)];
	}

	public async Task<IReadOnlyList<MediaId>> RemoveOwnedAsync(IEnumerable<MediaId> mediaIds, FeedId owner, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return [];
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var owned = ctx.Watchlist.Where(w => rawIds.Contains(w.Media.Id) && w.SourceFeedId == owner.Value && w.Origin == WatchlistEntryOrigin.Feed);
		var removed = await owned.Select(w => w.Media.Id).ToListAsync(cancellationToken);
		await owned.ExecuteDeleteAsync(cancellationToken);
		return [.. removed.Select(MediaId.From)];
	}
}
