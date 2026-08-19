using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal sealed class WatchlistOutboxRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<WatchlistOutboxRow>(builder =>
		{
			builder.HasUserOwner();
			builder.Property<Guid>("MediaId");
			builder.HasKey(UserOwnedConfigurationExtensions.UserIdColumn, "MediaId");
			builder.HasOne(o => o.Media).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(o => o.Action);
		});
}
