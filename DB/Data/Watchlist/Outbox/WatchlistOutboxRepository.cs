using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Watchlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal sealed class WatchlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWatchlistOutbox
{
	public async Task<IReadOnlyList<WatchlistOutboxEntry>> GetAsync(CancellationToken cancellationToken = default)
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
				outboxEntry.AddedAt,
			}
		).ToListAsync(cancellationToken);
		return rows.Select(row => new WatchlistOutboxEntry(MediaId.From(row.MediaIdValue), row.Kind, row.Status, row.Attempts) { AddedAt = row.AddedAt }).ToList();
	}

	public async Task<WatchlistOutboxEntry?> FindPendingAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var rawId = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx
			.WatchlistOutbox.AsNoTracking()
			.Where(o => o.Media.Id == rawId && o.Status == WatchlistOutboxState.Pending)
			.Select(o => new { Kind = o.Action, o.Attempts })
			.FirstOrDefaultAsync(cancellationToken);
		return row is null ? null : new WatchlistOutboxEntry(mediaId, row.Kind, WatchlistOutboxState.Pending, row.Attempts);
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
		var addedAtByMediaId = await ctx
			.Watchlist.Where(w => rawIds.Contains(w.Media.Id))
			.Select(w => new { w.Media.Id, w.CreatedAt })
			.ToDictionaryAsync(entry => entry.Id, entry => entry.CreatedAt, cancellationToken);

		foreach (var media in matchedMedia)
		{
			var addedAt = addedAtByMediaId.GetValueOrDefault(media.Id);
			if (existingByMediaId.TryGetValue(media.Id, out var existing))
			{
				existing.Action = action;
				existing.Status = WatchlistOutboxState.Pending;
				existing.Attempts = 0;
				existing.AddedAt = addedAt;
				continue;
			}
			ctx.WatchlistOutbox.Add(
				new WatchlistOutboxRow
				{
					Media = media,
					Action = action,
					Status = WatchlistOutboxState.Pending,
					AddedAt = addedAt,
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

	public async Task RecordFailedAttemptAsync(IEnumerable<MediaId> mediaIds, WatchlistOutboxState resultingState, CancellationToken cancellationToken = default)
	{
		var rawIds = mediaIds.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx
			.WatchlistOutbox.Where(o => rawIds.Contains(o.Media.Id))
			.ExecuteUpdateAsync(s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1).SetProperty(o => o.Status, resultingState), cancellationToken);
	}
}
