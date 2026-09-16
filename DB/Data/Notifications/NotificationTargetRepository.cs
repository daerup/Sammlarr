using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Notifications;

internal sealed class NotificationTargetRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : INotificationTargetRepository
{
	public async Task<IReadOnlyList<NotificationTarget>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.NotificationTargets.AsNoTracking().ToListAsync(cancellationToken);
		return rows.Select(Map).ToList();
	}

	public async Task<NotificationTarget?> FindAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.NotificationTargets.AsNoTracking().FirstOrDefaultAsync(target => target.Id == id, cancellationToken);
		return row is null ? null : Map(row);
	}

	public async Task<Guid> CreateAsync(NotificationTarget target, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = new NotificationTargetRow
		{
			Name = target.Name,
			Channel = target.Channel,
			Enabled = target.Enabled,
		};
		ApplyConfig(row, target.Config);
		ctx.NotificationTargets.Add(row);
		await ctx.SaveChangesAsync(cancellationToken);
		return row.Id;
	}

	public async Task UpdateAsync(NotificationTarget target, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.NotificationTargets.FirstOrDefaultAsync(existing => existing.Id == target.Id, cancellationToken);
		if (row is null)
		{
			return;
		}
		row.Name = target.Name;
		row.Channel = target.Channel;
		row.Enabled = target.Enabled;
		ApplyConfig(row, target.Config);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.NotificationTargets.Where(target => target.Id == id).ExecuteDeleteAsync(cancellationToken);
	}

	private static NotificationTarget Map(NotificationTargetRow row) => new(row.Id, row.Name, row.Enabled, BuildConfig(row));

	private static NotificationConfig BuildConfig(NotificationTargetRow row) =>
		row.Channel switch
		{
			NotificationChannel.Webhook => new WebhookConfig(row.Url ?? string.Empty, row.SigningSecret ?? string.Empty),
			NotificationChannel.Pushover => new PushoverConfig(row.AppToken ?? string.Empty, row.UserKey ?? string.Empty),
			NotificationChannel.WebUi => new WebUiConfig(),
		};

	private static void ApplyConfig(NotificationTargetRow row, NotificationConfig config) =>
		(row.Url, row.SigningSecret, row.AppToken, row.UserKey) = config switch
		{
			WebhookConfig webhook => (webhook.Url, webhook.SigningSecret, (string?)null, (string?)null),
			PushoverConfig pushover => ((string?)null, (string?)null, pushover.AppToken, pushover.UserKey),
			WebUiConfig => ((string?)null, (string?)null, (string?)null, (string?)null),
		};
}
