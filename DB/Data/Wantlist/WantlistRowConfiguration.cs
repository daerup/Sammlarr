using Microsoft.EntityFrameworkCore;

namespace DB.Data.Wantlist;

internal sealed class WantlistRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<WantlistRow>(builder =>
		{
			builder.HasUserOwner();
			builder.Property<Guid>("MediaId");
			builder.HasKey(UserOwnedConfigurationExtensions.UserIdColumn, "MediaId");
			builder.HasOne(w => w.Media).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.Restrict);
			builder.HasOne(w => w.SourceFeed).WithMany().HasForeignKey(w => w.SourceFeedId).OnDelete(DeleteBehavior.SetNull);
			builder.HasIndex(w => w.SourceFeedId);
			builder.HasOne(w => w.SourceRun).WithMany().HasForeignKey(w => w.SourceRunId).OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(w => w.SourceRunId);
		});
}
