using Contracts.Watchlist.Outbox;
using DB.Data.Media;
using Domain.Media;
using Domain.Watchlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal sealed class WatchlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWatchlistOutbox
{
	public async Task<IReadOnlyList<WatchlistDelivery>> GetAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from outboxEntry in ctx.WatchlistOutbox.AsNoTracking()
			select new
			{
				MediaIdValue = outboxEntry.Media.Id,
				Kind = outboxEntry.Action,
				outboxEntry.Status,
				outboxEntry.Attempts,
			}
		).ToListAsync(cancellationToken);
		return rows.Select(row => new WatchlistDelivery(MediaId.From(row.MediaIdValue), row.Kind, row.Status, row.Attempts)).ToList();
	}

	public async Task<WatchlistDelivery?> FindPendingAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var rawId = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx
			.WatchlistOutbox.AsNoTracking()
			.Where(o => o.Media.Id == rawId && o.Status == WatchlistDeliveryStatus.Pending)
			.Select(o => new { Kind = o.Action, o.Attempts })
			.FirstOrDefaultAsync(cancellationToken);
		return row is null ? null : new WatchlistDelivery(mediaId, row.Kind, WatchlistDeliveryStatus.Pending, row.Attempts);
	}

	public async Task EnqueueAsync(WatchlistOperationAction action, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return;
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var matchedMedia = await ctx.Media.Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var existingByMediaId = await ctx.WatchlistOutbox.Where(o => rawIds.Contains(o.Media.Id)).ToDictionaryAsync(o => o.Media.Id, cancellationToken);

		foreach (var media in matchedMedia)
		{
			if (existingByMediaId.TryGetValue(media.Id, out var existing))
			{
				existing.Action = action;
				existing.Status = WatchlistDeliveryStatus.Pending;
				existing.Attempts = 0;
				continue;
			}
			ctx.WatchlistOutbox.Add(
				new WatchlistOutboxRow
				{
					Media = media,
					Action = action,
					Status = WatchlistDeliveryStatus.Pending,
				}
			);
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DropAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var rawIds = mediaIds.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.WatchlistOutbox.Where(o => rawIds.Contains(o.Media.Id)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task RecordFailureAsync(IEnumerable<MediaId> mediaIds, WatchlistDeliveryStatus status, CancellationToken cancellationToken = default)
	{
		var rawIds = mediaIds.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx
			.WatchlistOutbox.Where(o => rawIds.Contains(o.Media.Id))
			.ExecuteUpdateAsync(s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1).SetProperty(o => o.Status, status), cancellationToken);
	}
}
