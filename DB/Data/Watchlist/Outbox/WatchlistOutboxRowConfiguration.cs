using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal sealed class WatchlistOutboxRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<WatchlistOutboxRow>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId");
			builder.HasOne(o => o.Media).WithOne().HasForeignKey<WatchlistOutboxRow>("MediaId").OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(o => o.Kind);
		});
}
