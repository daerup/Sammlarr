using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal sealed class BlacklistRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<BlacklistRow>(builder =>
		{
			builder.HasUserOwner();
			builder.Property<Guid>("MediaId");
			builder.HasKey(UserOwnedConfigurationExtensions.UserIdColumn, "MediaId");
			builder.HasOne(b => b.Media).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.Restrict);
		});
}
