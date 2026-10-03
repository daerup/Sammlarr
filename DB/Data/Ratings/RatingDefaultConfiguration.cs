using DB.Conversion.Ratings;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Ratings;

internal sealed class RatingDefaultConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RatingDefaultRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn).IsUnique();
			builder.Property(row => row.Strategy).HasConversion(new RatingStrategyConverter());
		});
	}
}
