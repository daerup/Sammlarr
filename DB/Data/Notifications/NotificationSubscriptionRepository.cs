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
				FromColumn(row.Level),
				FromColumn(row.Priority)
			)
		);

	private static void ApplyTemplate(NotificationSubscriptionRow row, NotificationTemplate template)
	{
		row.BodyTemplate = ToColumn(template.Body);
		row.TitleTemplate = ToColumn(template.Title);
		row.MessageTemplate = ToColumn(template.Message);
		row.Level = ToColumn(template.Level);
		row.Priority = ToColumn(template.Priority);
	}

	private static string? ToColumn(Optional<string> value) => value is Some<string> some ? some.Value : null;

	private static T? ToColumn<T>(Optional<T> value)
		where T : struct => value is Some<T> some ? some.Value : null;

	private static Optional<T> FromColumn<T>(T? value)
		where T : struct => value is { } present ? Some.Of(present) : new None();
}
