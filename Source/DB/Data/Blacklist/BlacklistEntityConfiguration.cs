using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal sealed class BlacklistEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<BlacklistEntity>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId");
			builder.HasOne(b => b.Media).WithOne().HasForeignKey<BlacklistEntity>("MediaId").OnDelete(DeleteBehavior.Restrict);
		});
}
