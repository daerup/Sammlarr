using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Wantlist.Outbox;

internal sealed class WantlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWantlistOutbox
{
	public async Task<IReadOnlyList<WantlistOutboxEntry>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from outboxEntry in ctx.WantlistOutbox.AsNoTracking()
			select new
			{
				MediaIdValue = outboxEntry.Media.Id,
				Kind = outboxEntry.Action,
				outboxEntry.Status,
				outboxEntry.Attempts,
				outboxEntry.LastError,
				outboxEntry.AddedAt,
			}
		).ToListAsync(cancellationToken);
		return rows.Select(row => new WantlistOutboxEntry(MediaId.From(row.MediaIdValue), row.Kind, row.Status, row.Attempts)
			{
				AddedAt = row.AddedAt,
				LastError = Optional.Of(row.LastError),
			})
			.ToList();
	}

	public async Task EnqueueAsync(WantlistOperationAction action, IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var distinct = ids.Distinct().ToList();
		if (distinct.Count == 0)
		{
			return;
		}
		var rawIds = distinct.Select(id => id.Value).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var matchedMedia = await ctx.Media.Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var existingByMediaId = await ctx.WantlistOutbox.Where(o => rawIds.Contains(o.Media.Id)).ToDictionaryAsync(o => o.Media.Id, cancellationToken);
		var addedAtByMediaId = await ctx
			.Wantlist.Where(w => rawIds.Contains(w.Media.Id))
			.Select(w => new { w.Media.Id, w.CreatedAt })
			.ToDictionaryAsync(entry => entry.Id, entry => entry.CreatedAt, cancellationToken);

		foreach (var media in matchedMedia)
		{
			var addedAt = addedAtByMediaId.GetValueOrDefault(media.Id);
			if (existingByMediaId.TryGetValue(media.Id, out var existing))
			{
				existing.Action = action;
				existing.Status = WantlistOutboxState.Pending;
				existing.Attempts = 0;
				existing.LastError = null;
				existing.AddedAt = addedAt;
				continue;
			}
			ctx.WantlistOutbox.Add(
				new WantlistOutboxRow
				{
					Media = media,
					Action = action,
					Status = WantlistOutboxState.Pending,
					AddedAt = addedAt,
				}
			);
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DropAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var rawIds = ids.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.WantlistOutbox.Where(o => rawIds.Contains(o.Media.Id)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task RecordFailedAttemptAsync(
		IEnumerable<MediaId> ids,
		WantlistOutboxState resultingState,
		Optional<string> reason,
		CancellationToken cancellationToken = default
	)
	{
		var rawIds = ids.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		var lastError = reason.ToNullable();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx
			.WantlistOutbox.Where(o => rawIds.Contains(o.Media.Id))
			.ExecuteUpdateAsync(
				s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1).SetProperty(o => o.Status, resultingState).SetProperty(o => o.LastError, lastError),
				cancellationToken
			);
	}
}
