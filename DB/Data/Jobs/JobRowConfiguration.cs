using DB.Conversion.Jobs;
using DB.Data.Identity;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Jobs;

internal sealed class JobRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<JobRow>(builder =>
		{
			builder.HasOne<UserRow>().WithMany().HasForeignKey(job => job.OwnerId).OnDelete(DeleteBehavior.Cascade);
			builder.Property(job => job.Request).HasConversion(new JobRequestConverter());
			builder.HasIndex(job => new { job.State, job.RunAfter });
			builder
				.HasIndex(job => new
				{
					job.Kind,
					job.Key,
					job.OwnerId,
				})
				.IsUnique()
				.HasFilter("\"State\" = 'Queued'");
		});
}
