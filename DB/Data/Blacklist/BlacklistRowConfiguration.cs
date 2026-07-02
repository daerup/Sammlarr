using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Blacklist;

internal sealed class BlacklistRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<BlacklistRow>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId");
			builder.HasOne(b => b.Media).WithOne().HasForeignKey<BlacklistRow>("MediaId").OnDelete(DeleteBehavior.Restrict);
		});
}
