using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Wantlist.Outbox;

internal sealed class WantlistOutboxRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IWantlistOutbox
{
	public async Task<IReadOnlyList<WantlistOutboxEntry>> GetAllAsync(SinkKind sink, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await (
			from outboxEntry in ctx.WantlistOutbox.AsNoTracking()
			where outboxEntry.Sink == sink
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

	public async Task EnqueueAsync(SinkKind sink, WantlistOperationAction action, IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var rawIds = ids.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await QueueAsync(ctx, sink, action, rawIds, cancellationToken);
	}

	public async Task ReplaceAsync(SinkKind sink, IEnumerable<MediaId> adds, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.WantlistOutbox.Where(o => o.Sink == sink).ExecuteDeleteAsync(cancellationToken);
		await QueueAsync(ctx, sink, WantlistOperationAction.Add, [.. adds.Select(id => id.Value).Distinct()], cancellationToken);
	}

	public async Task DropAsync(SinkKind sink, IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var rawIds = ids.Select(id => id.Value).Distinct().ToList();
		if (rawIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.WantlistOutbox.Where(o => o.Sink == sink && rawIds.Contains(o.Media.Id)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task DropOtherSinksAsync(SinkKind sink, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.WantlistOutbox.Where(o => o.Sink != sink).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task RecordFailedAttemptAsync(
		SinkKind sink,
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
			.WantlistOutbox.Where(o => o.Sink == sink && rawIds.Contains(o.Media.Id))
			.ExecuteUpdateAsync(
				s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1).SetProperty(o => o.Status, resultingState).SetProperty(o => o.LastError, lastError),
				cancellationToken
			);
	}

	private static async Task QueueAsync(
		SammlarrDbContext ctx,
		SinkKind sink,
		WantlistOperationAction action,
		List<Guid> rawIds,
		CancellationToken cancellationToken
	)
	{
		var matchedMedia = await ctx.Media.Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var existingByMediaId = await ctx
			.WantlistOutbox.Where(o => o.Sink == sink && rawIds.Contains(o.Media.Id))
			.ToDictionaryAsync(o => o.Media.Id, cancellationToken);
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
					Sink = sink,
					Status = WantlistOutboxState.Pending,
					AddedAt = addedAt,
				}
			);
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}
}
