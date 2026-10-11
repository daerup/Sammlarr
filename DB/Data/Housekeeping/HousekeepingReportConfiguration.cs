using Microsoft.EntityFrameworkCore;

namespace DB.Data.Housekeeping;

internal sealed class HousekeepingReportConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<HousekeepingReportRow>(builder =>
		{
			builder.HasKey(row => row.Id);
			builder.Property(row => row.Id).ValueGeneratedNever();
			builder.HasMany(row => row.Steps).WithOne().HasForeignKey(step => step.ReportId).OnDelete(DeleteBehavior.Cascade);
		});
		modelBuilder.Entity<HousekeepingStepRow>(builder => builder.HasKey(step => new { step.ReportId, step.Step }));
	}
}
