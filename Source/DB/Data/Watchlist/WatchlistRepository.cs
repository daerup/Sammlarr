using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist;

internal class WatchlistRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IMediaRepository mediaRepository) : IWatchlistRepository
{
	public async Task<IReadOnlyList<WatchlistedMedia>> GetEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from externalId in ctx.ExternalMediaIds.AsNoTracking().WhereSource(mediaProvider)
			join watchlistEntry in ctx.Watchlist.AsNoTracking().Include(w => w.SourceFeed) on externalId.Media.Id equals watchlistEntry.Media.Id
			select new { ExternalId = externalId, WatchlistEntry = watchlistEntry }
		).ToListAsync(cancellationToken);

		return [.. rows.Select(row => new WatchlistedMedia(row.ExternalId.ToMediaId(), row.WatchlistEntry.CreatedAt, row.WatchlistEntry.ToProvenance()))];
	}

	public async Task<IReadOnlyList<IMediaId>> GetIdsAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var externalIds = await (
			from externalId in ctx.ExternalMediaIds.AsNoTracking().WhereSource(mediaProvider)
			join watchlistEntry in ctx.Watchlist.AsNoTracking() on externalId.Media.Id equals watchlistEntry.Media.Id
			select externalId
		).ToListAsync(cancellationToken);
		return [.. externalIds.Select(e => e.ToMediaId())];
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
			await mediaRepository.UpsertAsync([id], cancellationToken);
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var matchedMedia = await ctx.MediaMatchingAny(ids).ToListAsync(cancellationToken);
		var mediaIdValues = matchedMedia.Select(m => m.Id).ToList();
		var existingMediaIds = await ctx.Watchlist.Where(w => mediaIdValues.Contains(w.Media.Id)).Select(w => w.Media.Id).ToHashSetAsync(cancellationToken);

		foreach (var media in matchedMedia.Where(m => !existingMediaIds.Contains(m.Id)))
		{
			var row = new WatchlistEntity { Media = media, Origin = WatchlistEntryOrigin.Manual };
			row.Apply(provenance);
			ctx.Watchlist.Add(row);
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
		var mediaIdValues = await ctx.MediaMatchingAny(ids).Select(m => m.Id).ToListAsync(cancellationToken);
		if (mediaIdValues.Count == 0)
		{
			return;
		}
		await ctx.Watchlist.Where(w => mediaIdValues.Contains(w.Media.Id)).ExecuteDeleteAsync(cancellationToken);
	}
}
