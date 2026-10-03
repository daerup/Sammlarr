using DB.Conversion.Feed;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed.Run;

internal sealed class FeedRunRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<FeedRunRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn);
			builder.HasIndex(x => new { x.FeedId, x.StartedAt });
			builder.Property(x => x.Strategy).HasConversion(new FeedStrategyConverter());
			builder.Property(x => x.Trigger).HasConversion(new FeedRunTriggerConverter());
			builder.HasOne<FeedRow>().WithMany().HasForeignKey(x => x.FeedId).OnDelete(DeleteBehavior.Cascade);
			builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.FeedRunId).OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<FeedRunItemRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn);
			builder.HasIndex(x => new { x.FeedRunId, x.MediaId }).IsUnique();
		});
	}
}
