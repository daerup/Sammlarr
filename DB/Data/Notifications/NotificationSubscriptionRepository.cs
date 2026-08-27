using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Notifications;

internal sealed class NotificationSubscriptionRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : INotificationSubscriptionRepository
{
	public async Task<IReadOnlyList<NotificationSubscription>> GetForTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.NotificationSubscriptions.AsNoTracking().Where(subscription => subscription.TargetId == targetId).ToListAsync(cancellationToken);
		return rows.Select(Map).ToList();
	}

	public async Task<IReadOnlyList<NotificationSubscription>> GetForEventTypeAsync(string eventType, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.NotificationSubscriptions.AsNoTracking().Where(subscription => subscription.EventType == eventType).ToListAsync(cancellationToken);
		return rows.Select(Map).ToList();
	}

	public async Task<Guid> AddAsync(Guid targetId, string eventType, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = new NotificationSubscriptionRow { TargetId = targetId, EventType = eventType };
		ctx.NotificationSubscriptions.Add(row);
		await ctx.SaveChangesAsync(cancellationToken);
		return row.Id;
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.NotificationSubscriptions.Where(subscription => subscription.Id == id).ExecuteDeleteAsync(cancellationToken);
	}

	private static NotificationSubscription Map(NotificationSubscriptionRow row) => new(row.Id, row.TargetId, row.EventType);
}
