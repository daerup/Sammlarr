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

	public async Task<IReadOnlyList<NotificationSubscription>> GetForEventTypeAsync(
		NotificationEventType eventType,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.NotificationSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
		return rows.Where(row => row.EventTypes.Contains(eventType)).Select(Map).ToList();
	}

	public async Task<Guid> AddAsync(
		Guid targetId,
		IReadOnlyList<NotificationEventType> eventTypes,
		NotificationTemplate template,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = new NotificationSubscriptionRow { TargetId = targetId, EventTypes = [.. eventTypes] };
		ApplyTemplate(row, template);
		ctx.NotificationSubscriptions.Add(row);
		await ctx.SaveChangesAsync(cancellationToken);
		return row.Id;
	}

	public async Task UpdateAsync(
		Guid id,
		IReadOnlyList<NotificationEventType> eventTypes,
		NotificationTemplate template,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.NotificationSubscriptions.FirstOrDefaultAsync(subscription => subscription.Id == id, cancellationToken);
		if (row is null)
		{
			return;
		}
		row.EventTypes = [.. eventTypes];
		ApplyTemplate(row, template);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.NotificationSubscriptions.Where(subscription => subscription.Id == id).ExecuteDeleteAsync(cancellationToken);
	}

	private static NotificationSubscription Map(NotificationSubscriptionRow row) =>
		new(
			row.Id,
			row.TargetId,
			row.EventTypes,
			new NotificationTemplate(
				Optional.Of(row.BodyTemplate),
				Optional.Of(row.TitleTemplate),
				Optional.Of(row.MessageTemplate),
				Optional.Of(row.Level),
				Optional.Of(row.Priority)
			)
		);

	private static void ApplyTemplate(NotificationSubscriptionRow row, NotificationTemplate template)
	{
		row.BodyTemplate = template.Body.ToNullable();
		row.TitleTemplate = template.Title.ToNullable();
		row.MessageTemplate = template.Message.ToNullable();
		row.Level = template.Level.ToNullable();
		row.Priority = template.Priority.ToNullable();
	}
}
