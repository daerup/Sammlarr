using Contracts.Media;
using Contracts.Watchlist.Outbox;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal class WatchlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IMediaRepository mediaRepository) : IWatchlistOutbox
{
	public async Task<IReadOnlyList<WatchlistOperation>> GetAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from externalId in ctx.ExternalMediaIds.AsNoTracking().WhereSource(mediaProvider)
			join outboxEntry in ctx.WatchlistOutbox.AsNoTracking() on externalId.Media.Id equals outboxEntry.Media.Id
			select new { ExternalId = externalId, outboxEntry.Kind }
		).ToListAsync(cancellationToken);
		return rows.Select(row => new WatchlistOperation(row.ExternalId.ToMediaId(), row.Kind)).ToList();
	}

	public async Task EnqueueAsync(WatchlistOperationKind kind, IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		IReadOnlyList<IMediaId> ids = [.. mediaIds];
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
		var existingByMediaId = await ctx.WatchlistOutbox.Where(o => mediaIdValues.Contains(o.Media.Id)).ToDictionaryAsync(o => o.Media.Id, cancellationToken);

		foreach (var media in matchedMedia)
		{
			if (existingByMediaId.TryGetValue(media.Id, out var existing))
			{
				existing.Kind = kind;
				continue;
			}
			ctx.WatchlistOutbox.Add(new WatchlistOutboxEntity { Media = media, Kind = kind });
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
		var mediaIdValues = await ctx.MediaMatchingAny(ids).Select(m => m.Id).ToListAsync(cancellationToken);
		if (mediaIdValues.Count == 0)
		{
			return;
		}
		await ctx.WatchlistOutbox.Where(o => mediaIdValues.Contains(o.Media.Id) && o.Kind == kind).ExecuteDeleteAsync(cancellationToken);
	}
}
