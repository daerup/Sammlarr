using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Notifications;

internal sealed class NotificationDeliveryOutcomeRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : INotificationDeliveryOutcomeRepository
{
	private const int MaxPerTarget = 20;

	public async Task RecordAsync(Guid eventId, Guid targetId, string eventType, DeliveryOutcome outcome, CancellationToken cancellationToken = default)
	{
		var (kind, reason, nextAttemptAt) = Flatten(outcome);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var row = await ctx.NotificationDeliveryOutcomes.FirstOrDefaultAsync(
			existing => existing.EventId == eventId && existing.TargetId == targetId,
			cancellationToken
		);
		if (row is null)
		{
			ctx.NotificationDeliveryOutcomes.Add(
				new NotificationDeliveryOutcomeRow
				{
					Id = Guid.CreateVersion7(),
					EventId = eventId,
					TargetId = targetId,
					EventType = eventType,
					Kind = kind,
					Reason = reason,
					NextAttemptAt = nextAttemptAt,
				}
			);
		}
		else
		{
			(row.Kind, row.Reason, row.NextAttemptAt) = (kind, reason, nextAttemptAt);
		}

		await ctx.SaveChangesAsync(cancellationToken);
		await PruneAsync(ctx, targetId, cancellationToken);
	}

	public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>> GetRecentByTargetAsync(
		int count,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.NotificationDeliveryOutcomes.AsNoTracking()
			.OrderByDescending(outcome => outcome.UpdatedAt)
			.ThenByDescending(outcome => outcome.Id)
			.ToListAsync(cancellationToken);
		return rows.GroupBy(outcome => outcome.TargetId)
			.ToDictionary(group => group.Key, group => (IReadOnlyList<NotificationDeliveryOutcome>)group.Take(count).Select(Map).ToList());
	}

	private static async Task PruneAsync(SammlarrDbContext ctx, Guid targetId, CancellationToken cancellationToken)
	{
		var keep = await ctx
			.NotificationDeliveryOutcomes.Where(outcome => outcome.TargetId == targetId)
			.OrderByDescending(outcome => outcome.UpdatedAt)
			.ThenByDescending(outcome => outcome.Id)
			.Take(MaxPerTarget)
			.Select(outcome => outcome.Id)
			.ToListAsync(cancellationToken);
		await ctx.NotificationDeliveryOutcomes.Where(outcome => outcome.TargetId == targetId && !keep.Contains(outcome.Id)).ExecuteDeleteAsync(cancellationToken);
	}

	private static NotificationDeliveryOutcome Map(NotificationDeliveryOutcomeRow row) => new(Rebuild(row), row.EventType, row.UpdatedAt);

	private static (OutcomeKind Kind, string? Reason, DateTimeOffset? NextAttemptAt) Flatten(DeliveryOutcome outcome) =>
		outcome switch
		{
			Delivered => (OutcomeKind.Delivered, null, null),
			Retrying retrying => (OutcomeKind.Retrying, retrying.Reason, retrying.NextAttemptAt),
			Failed failed => (OutcomeKind.Failed, failed.Reason, null),
		};

	private static DeliveryOutcome Rebuild(NotificationDeliveryOutcomeRow row) =>
		row.Kind switch
		{
			OutcomeKind.Delivered => new Delivered(),
			OutcomeKind.Retrying => new Retrying(row.Reason ?? string.Empty, row.NextAttemptAt.GetValueOrDefault()),
			OutcomeKind.Failed => new Failed(row.Reason ?? string.Empty),
		};
}
