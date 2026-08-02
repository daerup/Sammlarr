using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.Media;
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

	public async Task AddAsync(IEnumerable<MediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var matchedMedia = await ctx.Media.Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var existingMediaIds = await ctx.Watchlist.Where(w => rawIds.Contains(w.Media.Id)).Select(w => w.Media.Id).ToHashSetAsync(cancellationToken);

		foreach (var media in matchedMedia.Where(m => !existingMediaIds.Contains(m.Id)))
		{
			ctx.Watchlist.Add(WatchlistEntryMapping.ToRow(media, provenance));
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return;
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Watchlist.Where(w => rawIds.Contains(w.Media.Id)).ExecuteDeleteAsync(cancellationToken);
	}
}
