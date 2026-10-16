using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blocklist;

internal sealed class BlocklistRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<BlocklistRow>(builder =>
		{
			builder.HasUserOwner();
			builder.Property<Guid>("MediaId");
			builder.HasKey(UserOwnedConfigurationExtensions.UserIdColumn, "MediaId");
			builder.HasOne(b => b.Media).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.Restrict);
		});
}
