using Microsoft.EntityFrameworkCore;

namespace DB.Data.Notifications;

internal sealed class NotificationModelConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<NotificationTargetRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn, nameof(NotificationTargetRow.Channel));
		});

		modelBuilder.Entity<NotificationSubscriptionRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(subscription => subscription.TargetId);
			builder.HasOne<NotificationTargetRow>().WithMany().HasForeignKey(subscription => subscription.TargetId).OnDelete(DeleteBehavior.Cascade);
		});
	}
}
