using Microsoft.EntityFrameworkCore;

namespace DB.Data.Usage;

internal sealed class UsageConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RemoteUsageRow>(builder => builder.HasIndex(row => row.Hour));
		modelBuilder.Entity<RemoteAttemptRow>(builder =>
		{
			builder.HasIndex(row => row.Hour);
			builder.HasMany(row => row.Buckets).WithOne().HasForeignKey("AttemptId").OnDelete(DeleteBehavior.Cascade);
		});
		modelBuilder.Entity<RemoteAttemptBucketRow>(builder =>
		{
			builder.Property<long>("AttemptId");
			builder.HasKey("AttemptId", nameof(RemoteAttemptBucketRow.UpToMs));
		});
	}
}
