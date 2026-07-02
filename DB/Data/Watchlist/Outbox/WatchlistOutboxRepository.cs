using Contracts.Media;
using Contracts.Watchlist.Outbox;
using DB.Data.Media;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal class WatchlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWatchlistOutbox
{
	public async Task<IReadOnlyList<WatchlistOperation>> GetAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (from outboxEntry in ctx.WatchlistOutbox.AsNoTracking() select new { MediaIdValue = outboxEntry.Media.Id, outboxEntry.Kind }).ToListAsync(
			cancellationToken
		);
		return rows.Select(row => new WatchlistOperation(MediaId.From(row.MediaIdValue), row.Kind)).ToList();
	}

	public async Task EnqueueAsync(WatchlistOperationKind kind, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
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
				existing.Kind = kind;
				continue;
			}
			ctx.WatchlistOutbox.Add(new WatchlistOutboxEntity { Media = media, Kind = kind });
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DropAsync(WatchlistOperationKind kind, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return;
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.WatchlistOutbox.Where(o => rawIds.Contains(o.Media.Id) && o.Kind == kind).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task RecordFailureAsync(WatchlistOperationKind kind, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return;
		}
		var rawIds = ids.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx
			.WatchlistOutbox.Where(o => rawIds.Contains(o.Media.Id) && o.Kind == kind)
			.ExecuteUpdateAsync(s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1), cancellationToken);
	}
}
